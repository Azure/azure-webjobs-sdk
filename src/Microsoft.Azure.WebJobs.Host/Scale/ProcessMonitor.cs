// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// Type was moved from https://github.com/Azure/azure-functions-host/blob/dev/src/WebJobs.Script/Scale/ProcessMonitor.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    internal class ProcessMonitor : IDisposable
    {
        internal const int SampleHistorySize = 10;
        internal const int DefaultSampleIntervalSeconds = 1;

        private readonly List<double> _cpuLoadHistory = new List<double>();
        private readonly List<long> _memoryUsageHistory = new List<long>();
        private readonly int _effectiveCores;
        private readonly bool _autoStart;
        private readonly IProcess _process;

        private Timer _timer;
        private TimeSpan? _lastProcessorTime;
        private DateTime _lastSampleTime;
        private bool _disposed = false;
        private TimeSpan? _interval;
        private object _syncLock = new object();

        // for mock testing only
        internal ProcessMonitor()
        {
        }

        public ProcessMonitor(Process process, TimeSpan? interval = null)
            : this(new ProcessWrapper(process), interval)
        {
        }

        public ProcessMonitor(IProcess process, TimeSpan? interval = null, int? effectiveCores = null, bool autoStart = true)
        {
            _process = process ?? throw new ArgumentNullException(nameof(process));
            _interval = interval ?? TimeSpan.FromSeconds(DefaultSampleIntervalSeconds);
            _effectiveCores = effectiveCores ?? Utility.GetEffectiveCoresCount();
            _autoStart = autoStart;
        }

        /// <summary>
        /// The process being monitored.
        /// </summary>
        public virtual IProcess Process => _process;

        internal void EnsureTimerStarted()
        {
            if (_timer == null)
            {
                lock (_syncLock)
                {
                    if (_timer == null)
                    {
                        _timer = new Timer(OnTimer, null, TimeSpan.Zero, _interval.Value);
                    }
                }
            }
        }

        public virtual ProcessStats GetStats()
        {
            if (_autoStart)
            {
                // we only start the timer on demand, ensuring that if stats aren't being queried,
                // we're not performing unnecessary background work
                EnsureTimerStarted();
            }

            ProcessStats stats = null;
            lock (_syncLock)
            {
                stats = new ProcessStats
                {
                    ProcessId = _process.Id,
                    CpuLoadHistory = _cpuLoadHistory.ToArray(),
                    MemoryUsageHistory = _memoryUsageHistory.ToArray()
                };
            }
            return stats;
        }

        private void OnTimer(object state)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (_process.HasExited)
                {
                    // stop monitoring the exited process
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                    return;
                }

                _process.Refresh();

                SampleProcessMetrics();
            }
            catch
            {
                // Don't allow background exceptions to escape
                // E.g. when a child process we're monitoring exits,
                // we may process exceptions until the timer stops.
            }
        }

        internal void SampleProcessMetrics()
        {
            var currSampleTime = DateTime.UtcNow;
            var currSampleDuration = currSampleTime - _lastSampleTime;

            SampleProcessMetrics(currSampleDuration);

            _lastSampleTime = currSampleTime;
        }

        internal void SampleProcessMetrics(TimeSpan currSampleDuration)
        {
            SampleCPULoad(currSampleDuration);
            SampleMemoryUsage();
        }

        internal void SampleCPULoad(TimeSpan currSampleDuration)
        {
            var currProcessorTime = _process.TotalProcessorTime;

            if (_lastProcessorTime != null)
            {
                double cpuLoad = CalculateCpuLoad(currSampleDuration, currProcessorTime, _lastProcessorTime.Value, _effectiveCores);
                AddSample(_cpuLoadHistory, cpuLoad);
            }

            _lastProcessorTime = currProcessorTime;
        }

        internal static double CalculateCpuLoad(TimeSpan sampleDuration, TimeSpan currProcessorTime, TimeSpan lastProcessorTime, int coreCount)
        {
            // total processor time used for this sample across all cores
            var currSampleProcessorTime = (currProcessorTime - lastProcessorTime).TotalMilliseconds;

            // max possible processor time for this sample across all cores
            var totalSampleProcessorTime = coreCount * sampleDuration.TotalMilliseconds;

            // percentage of the max is our actual usage
            double cpuLoad = currSampleProcessorTime / totalSampleProcessorTime;
            cpuLoad = Math.Round(cpuLoad * 100);

            // in some high load scenarios or when the host is undergoing thread pool starvation
            // we've seen the above calculation return > 100. So we apply a Min here so our load
            // is never above 100%.
            return Math.Min(100, cpuLoad);
        }

        internal void SampleMemoryUsage()
        {
            AddSample(_memoryUsageHistory, _process.PrivateMemoryBytes);
        }

        private void AddSample<T>(List<T> samples, T sample)
        {
            lock (_syncLock)
            {
                if (samples.Count == SampleHistorySize)
                {
                    samples.RemoveAt(0);
                }
                samples.Add(sample);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _timer?.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Interface over a system Process. Useful for mock testing because
        /// Process isn't mockable.
        /// </summary>
        internal interface IProcess
        {
            int Id { get; }
            bool HasExited { get; }
            TimeSpan TotalProcessorTime { get; }
            long PrivateMemoryBytes { get; }
            void Refresh();
        }

        internal class ProcessWrapper : IProcess
        {
            private readonly Process _process;

            public ProcessWrapper(Process process)
            {
                _process = process ?? throw new ArgumentNullException(nameof(process));
            }

            public int Id => _process.Id;

            public bool HasExited
            {
                get
                {
                    try
                    {
                        return _process.HasExited;
                    }
                    catch
                    {
                        // When the process has been Closed/Disposed
                        // Process members like HasExited will throw
                        return true;
                    }
                }
            }

            public TimeSpan TotalProcessorTime => _process.TotalProcessorTime;

            public long PrivateMemoryBytes => _process.PrivateMemorySize64;

            public void Refresh()
            {
                _process.Refresh();
            }

            public override bool Equals(object obj)
            {
                if (obj == null)
                {
                    return false;
                }

                // when comparing to Process instance, we're equal if we wrap
                // the same process
                Process otherProcess = obj as Process;
                if (otherProcess != null)
                {
                    return object.ReferenceEquals(_process, otherProcess);
                }

                // when comparing to another ProcessWrapper, we're equal if we're
                // both pointing to the same process
                ProcessWrapper otherWrapper = obj as ProcessWrapper;
                if (otherWrapper != null)
                {
                    return object.ReferenceEquals(_process, otherWrapper._process);
                }

                return base.Equals(obj);
            }

            public override int GetHashCode()
            {
                return _process.GetHashCode();
            }
        }
    }
}
