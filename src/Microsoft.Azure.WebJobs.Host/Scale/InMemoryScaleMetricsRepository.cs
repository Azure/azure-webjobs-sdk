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
    public class InMemoryScaleMetricsRepository : IScaleMetricsRepository
    {
        private readonly ConcurrentDictionary<IScaleMonitor, IList<ScaleMetrics>> _monitorMetrics;
        private readonly ILogger _logger;
        private readonly IOptions<ScaleOptions> _scaleOptions;

        public InMemoryScaleMetricsRepository(IOptions<ScaleOptions> scaleOptions, ILoggerFactory loggerFactory)
        {
            _monitorMetrics = new ConcurrentDictionary<IScaleMonitor, IList<ScaleMetrics>>();
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
                    foreach (ScaleMetrics metric in pair.Value)
                    {
                        if (metric.Timestamp < threshold)
                        {
                            bool removed = _monitorMetrics.TryRemove(pair.Key, out _);
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
                    _monitorMetrics.TryGetValue(monitor, out metrics);
                }
                result.Add(monitor, metrics ?? new List<ScaleMetrics>());
            }
            return Task.FromResult(result);
        }

        public Task WriteMetricsAsync(IDictionary<IScaleMonitor, ScaleMetrics> monitorMetrics)
        {
            foreach (IScaleMonitor scaleMonitor in monitorMetrics.Keys)
            {
                if (_monitorMetrics.TryGetValue(scaleMonitor, out IList<ScaleMetrics> metrics))
                {
                    metrics.Add(monitorMetrics[scaleMonitor]);
                }
                else
                {
                    if (!_monitorMetrics.TryAdd(scaleMonitor, new List<ScaleMetrics>() { monitorMetrics[scaleMonitor] }))
                    {
                        _logger.LogWarning($"Monitor {scaleMonitor.Descriptor.Id} already exists.");
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
