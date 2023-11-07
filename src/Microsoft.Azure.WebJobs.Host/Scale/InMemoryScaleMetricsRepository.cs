// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Class that implements <see cref="IScaleMetricsRepository"/> to reading/writing scale metrics from memory.
    /// </summary>
    internal class InMemoryScaleMetricsRepository : IScaleMetricsRepository
    {
        private readonly ConcurrentDictionary<IScaleMonitor, ConcurrentDictionary<ScaleMetrics, object>> _monitorMetrics;
        private readonly ILogger _logger;
        private readonly IOptions<ScaleOptions> _scaleOptions;

        public InMemoryScaleMetricsRepository(IOptions<ScaleOptions> scaleOptions, ILoggerFactory loggerFactory)
        {
            _monitorMetrics = new ConcurrentDictionary<IScaleMonitor, ConcurrentDictionary<ScaleMetrics, object>>();
            _scaleOptions = scaleOptions;
            _logger = loggerFactory.CreateLogger<InMemoryScaleMetricsRepository>();
        }

        public Task<IDictionary<IScaleMonitor, IList<ScaleMetrics>>> ReadMetricsAsync(IEnumerable<IScaleMonitor> monitors)
        {
            // Purge old metrics
            if (_scaleOptions.Value.MetricsPurgeEnabled)
            {
                DateTime threshold = DateTime.UtcNow - _scaleOptions.Value.ScaleMetricsMaxAge;

                foreach (var pair in _monitorMetrics)
                {
                    if (_monitorMetrics.TryGetValue(pair.Key, out var metrics))
                    {
                        foreach (var metric in metrics.Keys)
                        {
                            if (metric.Timestamp < threshold)
                            {
                                if (!metrics.TryRemove(metric, out _))
                                {
                                    _logger.LogWarning($"A metric for ${pair.Key.Descriptor.Id} was not removed successfully");
                                }
                            }
                        }
                    }
                }
            }

            IDictionary<IScaleMonitor, IList<ScaleMetrics>> result = new Dictionary<IScaleMonitor, IList<ScaleMetrics>>();
            foreach (IScaleMonitor monitor in monitors)
            {
                Type metricsType = GetMonitorScaleMetricsTypeOrNull(monitor);
                IList<ScaleMetrics> metrics = null;
                if (metricsType != null)
                {
                    if (_monitorMetrics.TryGetValue(monitor, out var exisitngMetrics))
                    {
                        metrics = exisitngMetrics.Keys.OrderBy(x => x.Timestamp).ToList();
                    }
                }
                result.Add(monitor, metrics ?? new List<ScaleMetrics>());
            }
            return Task.FromResult(result);
        }

        public Task WriteMetricsAsync(IDictionary<IScaleMonitor, ScaleMetrics> monitorMetrics)
        {
            foreach (var pair in monitorMetrics)
            {
                if (_monitorMetrics.TryGetValue(pair.Key, out ConcurrentDictionary<ScaleMetrics, object> existingMetrics))
                {
                    if (!existingMetrics.TryAdd(pair.Value, null))
                    {
                        _logger.LogWarning($"A metric for ${pair.Key.Descriptor.Id} was not added successfully");
                    }
                }
                else
                {
                    var metrics = new ConcurrentDictionary<ScaleMetrics, object>();
                    metrics.TryAdd(pair.Value, null);
                    if (!_monitorMetrics.TryAdd(pair.Key, metrics))
                    {
                        _logger.LogWarning($"A metric for ${pair.Key.Descriptor.Id} was not added successfully");
                    }
                }
            }
            return Task.CompletedTask;
        }

        internal Type GetMonitorScaleMetricsTypeOrNull(IScaleMonitor monitor)
        {
            var monitorInterfaceType = monitor.GetType().GetInterfaces().SingleOrDefault(p => p.IsGenericType && p.GetGenericTypeDefinition() == typeof(IScaleMonitor<>));
            if (monitorInterfaceType != null)
            {
                return monitorInterfaceType.GetGenericArguments()[0];
            }
            // we require the monitor to implement the generic interface in order to know
            // what type to deserialize into
            _logger.LogWarning($"Monitor {monitor.GetType().FullName} doesn't implement {typeof(IScaleMonitor<>)}.");
            return null;
        }
    }
}
