// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Configuration for scaling.
    /// </summary>
    public class ScaleOptions
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

        public string Format()
        {
            var options = new JObject
            {
                { nameof(ScaleMetricsMaxAge), ScaleMetricsMaxAge },
                { nameof(ScaleMetricsSampleInterval), ScaleMetricsSampleInterval }
            };

            return options.ToString(Formatting.Indented);
        }

        /// <summary>
        /// Gets or sets if target base scale is enabled.
        /// </summary>
        public bool IsTargetBasedScalingEnabled { get; set; }

        /// <summary>
        /// Gets or sets the function to checks if target scaler is supported for specific <see cref="ITargetScaler"/>
        /// </summary>
        public Func<ITargetScaler, bool> IsTargetBasedScalingEnabledForTriggerFunc { get; set; }
    }
}
