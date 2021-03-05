// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    public interface IHostHealthMonitor
    {
        HostHealthStatus GetStatus(ILogger logger = null);
    }

    internal class DefaultHostHealthMonitor : IHostHealthMonitor, IDisposable
    {
        private ProcessMonitor _processMonitor;
        private const int MinSampleCount = 5;
        private const float _maxCpuThreshold = 0.70F;
        private DateTime _lastSampleTime;
        private bool _disposed;

        public DefaultHostHealthMonitor()
        {
            _processMonitor = new ProcessMonitor(Process.GetCurrentProcess());
            _processMonitor.Start();
            _lastSampleTime = DateTime.UtcNow;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _processMonitor?.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public HostHealthStatus GetStatus(ILogger logger = null)
        {
            HostHealthStatus status = HostHealthStatus.Unknown;

            var hostProcessStats = _processMonitor.GetStats();

            int hostProcessCpuStatsCount = hostProcessStats.CpuLoadHistory.Count();
            if (hostProcessCpuStatsCount > MinSampleCount)
            {
                // compute the aggregate average CPU usage for host for the last MinSampleCount samples
                var samples = hostProcessStats.CpuLoadHistory.Skip(hostProcessCpuStatsCount - MinSampleCount).Take(MinSampleCount);
                var hostAverageCpu = samples.Average();
                var aggregateAverage = Math.Round(hostAverageCpu);
                //logger?.HostAggregateCpuLoad(aggregateAverage);

                if (logger != null)
                {
                    string formattedLoadHistory = string.Join(",", samples);
                    logger?.LogInformation($"CPU Stats: (Avg. {aggregateAverage}) {formattedLoadHistory}");
                }

                // if the average is above our threshold, return true (we're overloaded)
                var adjustedThreshold = _maxCpuThreshold * 100;
                if (aggregateAverage >= adjustedThreshold)
                {
                    //logger?.HostCpuThresholdExceeded(aggregateAverage, adjustedThreshold);
                    status = HostHealthStatus.Overloaded;
                }
                else
                {
                    status = HostHealthStatus.Ok;
                }
            }

            return status;
        }
    }
}
