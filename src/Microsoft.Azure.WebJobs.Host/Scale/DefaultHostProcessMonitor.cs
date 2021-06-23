// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// Some of the logic in this file was moved from https://github.com/Azure/azure-functions-host/blob/852eed9ceef6ef56b431428a8eb31f1bd9c97f3b/src/WebJobs.Script/Scale/HostPerformanceManager.cs 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    internal class DefaultHostProcessMonitor : IHostProcessMonitor, IDisposable
    {
        internal const string CpuLimitName = "CPU";
        internal const string MemoryLimitName = "Memory";
        internal const int MinSampleCount = 5;

        private readonly long _maxMemoryThresholdBytes;
        private readonly ProcessMonitor _hostProcessMonitor;
        private readonly List<ProcessMonitor> _childProcessMonitors = new List<ProcessMonitor>();
        private readonly IOptions<ConcurrencyOptions> _options;
        private readonly object _syncLock = new object();

        private bool _disposed;

        public DefaultHostProcessMonitor(IOptions<ConcurrencyOptions> options, ProcessMonitor processMonitor = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _hostProcessMonitor = processMonitor ?? new ProcessMonitor(Process.GetCurrentProcess());

            if (_options.Value.MemoryThrottleEnabled)
            {
                _maxMemoryThresholdBytes = (long) (_options.Value.TotalAvailableMemoryBytes * (double)_options.Value.MemoryThreshold);
            }
        }

        // for testing
        internal List<ProcessMonitor> ChildProcessMonitors => _childProcessMonitors;

        public void RegisterChildProcess(Process process)
        {
            if (process == null)
            {
                throw new ArgumentNullException(nameof(process));
            }

            var monitor = new ProcessMonitor(process);
            RegisterChildProcessMonitor(monitor);
        }

        internal void RegisterChildProcessMonitor(ProcessMonitor monitor)
        {
            lock (_syncLock)
            {
                _childProcessMonitors.Add(monitor);
            }
        }

        public void UnregisterChildProcess(Process process)
        {
            if (process == null)
            {
                throw new ArgumentNullException(nameof(process));
            }

            ProcessMonitor monitor = null;
            lock (_syncLock)
            {
                // Try to locate the monitor for the specified process and remove it if found.
                // Note: use of Equals rather than == is important here since ProcessWrapper
                // overrides equality.
                monitor = _childProcessMonitors.SingleOrDefault(p => p.Process.Equals(process));
                if (monitor != null)
                {
                    _childProcessMonitors.Remove(monitor);
                }
            }

            monitor?.Dispose();
        }

        public HostProcessStatus GetStatus(ILogger logger = null)
        {
            var healthResult = new HostProcessStatus
            {
                State = HostHealthState.Unknown
            };

            // get the current stats for the host process
            ProcessStats hostProcessStats = _hostProcessMonitor.GetStats();

            // get the current stats for any child processes
            RemoveExitedChildProcesses();
            ProcessMonitor[] currChildProcessMonitors;
            lock (_syncLock)
            {
                // snapshot the current set of child monitors
                currChildProcessMonitors = _childProcessMonitors.ToArray();
            }
            IEnumerable<ProcessStats> childProcessStats = currChildProcessMonitors.Select(p => p.GetStats()).ToList();

            var exceededLimits = new List<string>();
            HostHealthState cpuStatus = GetCpuStatus(hostProcessStats, childProcessStats, logger);
            var statuses = new List<HostHealthState>
            {
                cpuStatus
            };
            if (cpuStatus == HostHealthState.Overloaded)
            {
                exceededLimits.Add(CpuLimitName);
            }

            HostHealthState memoryStatus = GetMemoryStatus(hostProcessStats, childProcessStats, logger);
            statuses.Add(memoryStatus);
            if (memoryStatus == HostHealthState.Overloaded)
            {
                exceededLimits.Add(MemoryLimitName);
            }

            if (statuses.All(p => p == HostHealthState.Unknown))
            {
                healthResult.State = HostHealthState.Unknown;
            }
            else if (statuses.Any(p => p == HostHealthState.Overloaded))
            {
                healthResult.State = HostHealthState.Overloaded;
            }
            else
            {
                healthResult.State = HostHealthState.Ok;
            }

            healthResult.ExceededLimits = exceededLimits.AsReadOnly();

            return healthResult;
        }

        private HostHealthState GetMemoryStatus(ProcessStats hostProcessStats, IEnumerable<ProcessStats> childProcessStats, ILogger logger = null)
        {
            HostHealthState status = HostHealthState.Unknown;

            // if memory throttling is not enabled return immediately
            if (!_options.Value.MemoryThrottleEnabled)
            {
                return status;
            }

            // First compute Memory usage for any registered child processes.
            // Here and below we wait until we get enough samples before making
            // any health decisions. This ensures that we've waited a short period
            // after startup to allow usage to stabilize.
            double currentChildMemoryUsageTotal = 0;
            foreach (var currentChildStats in childProcessStats.Where(p => p.MemoryUsageHistory.Count() >= MinSampleCount))
            {
                // take the last N samples
                int currChildProcessMemoryStatsCount = currentChildStats.MemoryUsageHistory.Count();
                var currChildMemoryStats = currentChildStats.MemoryUsageHistory.TakeLastN(MinSampleCount);
                var currChildMemoryStatsAverage = currChildMemoryStats.Average();

                currentChildMemoryUsageTotal += currChildMemoryStats.Last();

                string formattedLoadHistory = string.Join(",", currChildMemoryStats);
                logger?.HostProcessMemoryUsage(currentChildStats.ProcessId, formattedLoadHistory, currChildMemoryStatsAverage, currChildMemoryStats.Max());
            }

            // calculate the aggregate usage across host + child processes
            int hostProcessMemoryStatsCount = hostProcessStats.MemoryUsageHistory.Count();
            if (hostProcessMemoryStatsCount >= MinSampleCount)
            {
                var lastSamples = hostProcessStats.MemoryUsageHistory.TakeLastN(MinSampleCount);

                string formattedUsageHistory = string.Join(",", lastSamples);
                var hostAverageMemoryUsageBytes = lastSamples.Average();
                logger?.HostProcessMemoryUsage(hostProcessStats.ProcessId, formattedUsageHistory, Math.Round(hostAverageMemoryUsageBytes), lastSamples.Max());

                // For memory limit, unlike cpu we don't want to compare the average against the limit,
                // we want to use the last/current. Memory needs to be a harder limit that we want to avoid hitting
                // otherwise the host could see OOM exceptions.
                double currentMemoryUsage = currentChildMemoryUsageTotal + lastSamples.Last();

                // compute the current total memory usage for host + children
                var percentageOfMax = (int)(100 * (currentMemoryUsage / _maxMemoryThresholdBytes));
                logger?.HostAggregateMemoryUsage(currentMemoryUsage, percentageOfMax);

                // if the average is above our threshold, return true (we're overloaded)
                if (currentMemoryUsage >= _maxMemoryThresholdBytes)
                {
                    // TODO: As part of enabling memory throttling review the use of GC.Collect here.
                    // https://github.com/Azure/azure-webjobs-sdk/issues/2733
                    GC.Collect();

                    logger?.HostMemoryThresholdExceeded(currentMemoryUsage, _maxMemoryThresholdBytes);
                    return HostHealthState.Overloaded;
                }
                else
                {
                    return HostHealthState.Ok;
                }
            }

            return status;
        }

        private HostHealthState GetCpuStatus(ProcessStats hostProcessStats, IEnumerable<ProcessStats> childProcessStats, ILogger logger = null)
        {
            HostHealthState status = HostHealthState.Unknown;

            // First compute CPU usage for any registered child processes.
            // here and below, we wait until we get enough samples before making
            // any health decisions, to ensure that we have enough data to make
            // an informed decision.
            double childAverageCpuTotal = 0;
            var averageChildCpuStats = new List<double>();
            foreach (var currentStatus in childProcessStats.Where(p => p.CpuLoadHistory.Count() >= MinSampleCount))
            {
                // take the last N samples
                int currChildProcessCpuStatsCount = currentStatus.CpuLoadHistory.Count();
                var currChildCpuStats = currentStatus.CpuLoadHistory.TakeLastN(MinSampleCount);
                var currChildCpuStatsAverage = currChildCpuStats.Average();
                averageChildCpuStats.Add(currChildCpuStatsAverage);

                string formattedLoadHistory = string.Join(",", currChildCpuStats);
                logger?.HostProcessCpuStats(currentStatus.ProcessId, formattedLoadHistory, currChildCpuStatsAverage, currChildCpuStats.Max());
            }
            childAverageCpuTotal = averageChildCpuStats.Sum();

            // Calculate the aggregate load of host + child processes
            int hostProcessCpuStatsCount = hostProcessStats.CpuLoadHistory.Count();
            if (hostProcessCpuStatsCount >= MinSampleCount)
            {
                var lastSamples = hostProcessStats.CpuLoadHistory.TakeLastN(MinSampleCount);

                string formattedLoadHistory = string.Join(",", lastSamples);
                var hostAverageCpu = lastSamples.Average();
                logger?.HostProcessCpuStats(hostProcessStats.ProcessId, formattedLoadHistory, Math.Round(hostAverageCpu), Math.Round(lastSamples.Max()));

                // compute the aggregate average CPU usage for host + children for the last MinSampleCount samples
                var aggregateAverage = Math.Round(hostAverageCpu + childAverageCpuTotal);
                logger?.HostAggregateCpuLoad(aggregateAverage);

                // if the average is above our threshold, return true (we're overloaded)
                var adjustedThreshold = _options.Value.CPUThreshold * 100;
                if (aggregateAverage >= adjustedThreshold)
                {
                    logger?.HostCpuThresholdExceeded(aggregateAverage, adjustedThreshold);
                    return HostHealthState.Overloaded;
                }
                else
                {
                    return HostHealthState.Ok;
                }
            }

            return status;
        }

        private void RemoveExitedChildProcesses()
        {
            var exitedChildMonitors = _childProcessMonitors.Where(p => p.Process.HasExited).ToArray();
            if (exitedChildMonitors.Length > 0)
            {
                lock (_syncLock)
                {
                    foreach (var exitedChildMonitor in exitedChildMonitors)
                    {
                        _childProcessMonitors.Remove(exitedChildMonitor);
                    }
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _hostProcessMonitor?.Dispose();

                    foreach (var childMonitor in _childProcessMonitors)
                    {
                        childMonitor?.Dispose();
                    }
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
