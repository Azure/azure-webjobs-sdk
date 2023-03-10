// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Options used to configure scale monitoring.
    /// </summary>
    public class ScaleOptions : IOptionsFormatter
    {
        private TimeSpan _scaleMetricsMaxAge;
        private TimeSpan _scaleMetricsSampleInterval;

        public ScaleOptions()
        {
            // At the default values, a single monitor will be generating 6 samples per minute
            // so at 2 minutes that's 12 samples
            // Assume a case of 100 functions in an app, each mapping to a monitor. Thats
            // 1200 samples to read from storage on each scale status request.
            ScaleMetricsMaxAge = TimeSpan.FromMinutes(2);
            ScaleMetricsSampleInterval = TimeSpan.FromSeconds(10);
            MetricsPurgeEnabled = true;
        }

        /// <summary>
        /// Gets or sets a value indicating the maximum age for metrics.
        /// Metrics that exceed this age will not be returned to monitors.
        /// </summary>
        public TimeSpan ScaleMetricsMaxAge
        {
            get
            {
                return _scaleMetricsMaxAge;
            }

            set
            {
                if (value < TimeSpan.FromMinutes(1) || value > TimeSpan.FromMinutes(5))
                {
                    throw new ArgumentOutOfRangeException(nameof(ScaleMetricsMaxAge));
                }
                _scaleMetricsMaxAge = value;
            }
        }

        /// <summary>
        /// Gets or sets the sampling interval for metrics.
        /// </summary>
        public TimeSpan ScaleMetricsSampleInterval
        {
            get
            {
                return _scaleMetricsSampleInterval;
            }

            set
            {
                if (value < TimeSpan.FromSeconds(1) || value > TimeSpan.FromSeconds(30))
                {
                    throw new ArgumentOutOfRangeException(nameof(ScaleMetricsSampleInterval));
                }
                _scaleMetricsSampleInterval = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether old metrics data
        /// will be auto purged.
        /// </summary>
        public bool MetricsPurgeEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether target base scaling is enabled at the host level.
        /// </summary>
        public bool IsTargetScalingEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether runtime scale monitoring is enabled at the host level.
        /// </summary>
        public bool IsRuntimeScalingEnabled { get; set; }

        public string Format()
        {
            // only log options if scaling is enabled
            if (IsRuntimeScalingEnabled || IsTargetScalingEnabled)
            {
                var options = new JObject
                {
                    { nameof(ScaleMetricsMaxAge), ScaleMetricsMaxAge },
                    { nameof(ScaleMetricsSampleInterval), ScaleMetricsSampleInterval },
                    { nameof(MetricsPurgeEnabled), MetricsPurgeEnabled },
                    { nameof(IsTargetScalingEnabled), IsTargetScalingEnabled },
                    { nameof(IsRuntimeScalingEnabled), IsRuntimeScalingEnabled }
                };

                return options.ToString(Formatting.Indented);
            }
            else
            {
                return null;
            }
        }
    }
}
