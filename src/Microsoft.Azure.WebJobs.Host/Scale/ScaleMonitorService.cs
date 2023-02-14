// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Linq;
using Microsoft.Extensions.Options;
using Microsoft.Azure.WebJobs.Hosting;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Class to store scale metrics.
    /// </summary>
    internal class ScaleMonitorService : IHostedService, IDisposable
    {
        private readonly Timer _timer;
        private readonly IScaleManager _scaleManager;
        private readonly IScaleMetricsRepository _metricsRepository;
        private readonly ILogger _logger;
        private readonly IOptions<ScaleOptions> _scaleOptions;
        private readonly IPrimaryHostStateProvider _primaryHostStateProvider;
        private bool _disposed;

        public ScaleMonitorService(IScaleManager scaleManager, IScaleMetricsRepository metricsRepository, IOptions<ScaleOptions> scaleOptions, IPrimaryHostStateProvider primaryHostStateProvider, ILoggerFactory loggerFactory)
        {
            _scaleManager = scaleManager;
            _metricsRepository = metricsRepository;
            _timer = new Timer(OnTimer, null, Timeout.Infinite, Timeout.Infinite);
            _logger = loggerFactory.CreateLogger<ScaleMonitorService>();
            _scaleOptions = scaleOptions;
            _primaryHostStateProvider = primaryHostStateProvider;
        }

        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            // start the timer by setting the due time
            _logger.LogInformation("Scale monitor service is started.");
            SetTimerInterval((int)_scaleOptions.Value.ScaleMetricsSampleInterval.TotalMilliseconds);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // stop the timer if it has been started
            _timer.Change(Timeout.Infinite, Timeout.Infinite);

            return Task.CompletedTask;
        }

        protected async virtual void OnTimer(object state)
        {
            if (_primaryHostStateProvider.IsPrimary)
            {
                await TakeMetricsSamplesAsync();
            }

            SetTimerInterval((int)_scaleOptions.Value.ScaleMetricsSampleInterval.TotalMilliseconds);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private async Task TakeMetricsSamplesAsync()
        {
            try
            {
                (_scaleManager as ScaleManager).GetScalersToSample(out List<IScaleMonitor> scaleMonitorsToProcess, out List<ITargetScaler> targetScalersToProcess);

                if (scaleMonitorsToProcess.Any())
                {
                    _logger.LogDebug($"Taking metrics samples for {scaleMonitorsToProcess.Count()} monitor(s).");

                    var metricsMap = new Dictionary<IScaleMonitor, ScaleMetrics>();
                    foreach (var monitor in scaleMonitorsToProcess)
                    {
                        ScaleMetrics metrics = null;
                        try
                        {
                            // take a metrics sample for each monitor
                            metrics = await monitor.GetMetricsAsync();
                            metricsMap[monitor] = metrics;

                            // log the metrics json to provide visibility into monitor activity
                            var json = JsonConvert.SerializeObject(metrics);
                            _logger.LogDebug($"Scale metrics sample for monitor '{monitor.Descriptor.Id}': {json}");
                        }
                        catch (Exception exc) when (!exc.IsFatal())
                        {
                            // if a particular monitor fails, log and continue
                            _logger.LogError(exc, $"Failed to collect scale metrics sample for monitor '{monitor.Descriptor.Id}'.");
                        }
                    }

                    if (metricsMap.Count > 0)
                    {
                        // persist the metrics samples
                        await _metricsRepository.WriteMetricsAsync(metricsMap);
                    }
                }
            }
            catch (Exception exc) when (!exc.IsFatal())
            {
                _logger.LogError(exc, "Failed to collect/persist scale metrics.");
            }
        }

        private void SetTimerInterval(int dueTime)
        {
            if (!_disposed)
            {
                var timer = _timer;
                if (timer != null)
                {
                    try
                    {
                        _timer.Change(dueTime, Timeout.Infinite);
                    }
                    catch (ObjectDisposedException)
                    {
                        // might race with dispose
                    }
                }
            }
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _timer.Dispose();
                }

                _disposed = true;
            }
        }
    }
}
