// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights.SnapshotCollector;
using Microsoft.ApplicationInsights.WindowsServer.Channel.Implementation;
using Microsoft.Azure.WebJobs.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    public class ApplicationInsightsLoggerOptions : IOptionsFormatter
    {
        /// <summary>
        /// Gets or sets Application Insights instrumentation key.
        /// </summary>
        public string InstrumentationKey { get; set; }

        /// <summary>
        /// Gets or sets sampling settings.
        /// </summary>
        public SamplingPercentageEstimatorSettings SamplingSettings { get; set; }

        /// <summary>
        /// Gets or sets snapshot collection options.
        /// </summary>
        public SnapshotCollectorConfiguration SnapshotConfiguration { get; set; }

        /// <summary>
        /// Gets or sets authentication key for Quick Pulse (Live Metrics).
        /// </summary>
        [Obsolete("Use LiveMetricsAuthenticationApiKey instead.")]
        public string QuickPulseAuthenticationApiKey
        {
            get
            {
                return LiveMetricsAuthenticationApiKey;
            }
            set
            {
                LiveMetricsAuthenticationApiKey = value;
            }
        }

        /// <summary>
        /// Gets or sets authentication key for Live Metrics.
        /// </summary>
        public string LiveMetricsAuthenticationApiKey { get; set; }

        /// <summary>
        /// Gets or sets the time to delay Quick Pulse (Live Metrics) initialization. Delaying this initialization
        /// can result in decreased application startup time. Default value is 15 seconds.
        /// </summary>
        [Obsolete("Use LiveMetricsInitializationDelay instead.")]
        public TimeSpan QuickPulseInitializationDelay {
            get
            {
                return LiveMetricsInitializationDelay;
            }
            set
            {
                LiveMetricsInitializationDelay = value;
            }
        }

        /// <summary>
        /// Gets or sets the time to delay Live Metrics initialization. Delaying this initialization
        /// can result in decreased application startup time. Default value is 15 seconds.
        /// </summary>
        public TimeSpan LiveMetricsInitializationDelay { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Gets or sets flag that enables Kudu performance counters collection.
        /// https://github.com/projectkudu/kudu/wiki/Perf-Counters-exposed-as-environment-variables.
        /// Enabled by default.
        /// </summary>
        public bool EnablePerformanceCountersCollection { get; set; } = true;

        /// <summary>
        /// Gets or sets the flag that enables live metrics collection.
        /// Enabled by default.
        /// </summary>
        public bool EnableLiveMetrics { get; set; } = true;

        /// <summary>
        /// Gets or sets the flag that enables dependency tracking.
        /// Enabled by default.
        /// </summary>
        public bool EnableDependencyTracking { get; set; } = true;

        /// <summary>
        /// Gets or sets HTTP request collection options. 
        /// </summary>
        public HttpAutoCollectionOptions HttpAutoCollectionOptions { get; set; } = new HttpAutoCollectionOptions();

        public string Format()
        {
            JObject sampling = null;
            if (SamplingSettings != null)
            {
                sampling = new JObject
                {
                    { nameof(SamplingPercentageEstimatorSettings.EvaluationInterval), SamplingSettings.EvaluationInterval },
                    { nameof(SamplingPercentageEstimatorSettings.InitialSamplingPercentage), SamplingSettings.InitialSamplingPercentage },
                    { nameof(SamplingPercentageEstimatorSettings.MaxSamplingPercentage), SamplingSettings.MaxSamplingPercentage },
                    { nameof(SamplingPercentageEstimatorSettings.MaxTelemetryItemsPerSecond), SamplingSettings.MaxTelemetryItemsPerSecond },
                    { nameof(SamplingPercentageEstimatorSettings.MinSamplingPercentage), SamplingSettings.MinSamplingPercentage },
                    { nameof(SamplingPercentageEstimatorSettings.MovingAverageRatio), SamplingSettings.MovingAverageRatio },
                    { nameof(SamplingPercentageEstimatorSettings.SamplingPercentageDecreaseTimeout), SamplingSettings.SamplingPercentageDecreaseTimeout },
                    { nameof(SamplingPercentageEstimatorSettings.SamplingPercentageIncreaseTimeout), SamplingSettings.SamplingPercentageIncreaseTimeout },
                };
            }

            JObject snapshot = null;
            if (SnapshotConfiguration != null)
            {
                snapshot = new JObject
                {
                    { nameof(SnapshotCollectorConfiguration.FailedRequestLimit), SnapshotConfiguration.FailedRequestLimit },
                    { nameof(SnapshotCollectorConfiguration.IsEnabled), SnapshotConfiguration.IsEnabled },
                    { nameof(SnapshotCollectorConfiguration.IsEnabledInDeveloperMode), SnapshotConfiguration.IsEnabledInDeveloperMode },
                    { nameof(SnapshotCollectorConfiguration.IsEnabledWhenProfiling), SnapshotConfiguration.IsEnabledWhenProfiling },
                    { nameof(SnapshotCollectorConfiguration.IsLowPrioritySnapshotUploader), SnapshotConfiguration.IsLowPrioritySnapshotUploader },
                    { nameof(SnapshotCollectorConfiguration.MaximumCollectionPlanSize), SnapshotConfiguration.MaximumCollectionPlanSize },
                    { nameof(SnapshotCollectorConfiguration.MaximumSnapshotsRequired), SnapshotConfiguration.MaximumSnapshotsRequired },
                    { nameof(SnapshotCollectorConfiguration.ProblemCounterResetInterval), SnapshotConfiguration.ProblemCounterResetInterval },
                    { nameof(SnapshotCollectorConfiguration.ProvideAnonymousTelemetry), SnapshotConfiguration.ProvideAnonymousTelemetry },
                    { nameof(SnapshotCollectorConfiguration.ReconnectInterval), SnapshotConfiguration.ReconnectInterval },
                    { nameof(SnapshotCollectorConfiguration.ShadowCopyFolder), SnapshotConfiguration.ShadowCopyFolder },
                    { nameof(SnapshotCollectorConfiguration.SnapshotInLowPriorityThread), SnapshotConfiguration.SnapshotInLowPriorityThread },
                    { nameof(SnapshotCollectorConfiguration.SnapshotsPerDayLimit), SnapshotConfiguration.SnapshotsPerDayLimit },
                    { nameof(SnapshotCollectorConfiguration.SnapshotsPerTenMinutesLimit), SnapshotConfiguration.SnapshotsPerTenMinutesLimit },
                    { nameof(SnapshotCollectorConfiguration.TempFolder), SnapshotConfiguration.TempFolder },
                    { nameof(SnapshotCollectorConfiguration.ThresholdForSnapshotting), SnapshotConfiguration.ThresholdForSnapshotting }
                };
            }

            JObject httpOptions = new JObject
            {
                { nameof(HttpAutoCollectionOptions.EnableHttpTriggerExtendedInfoCollection), HttpAutoCollectionOptions.EnableHttpTriggerExtendedInfoCollection },
                { nameof(HttpAutoCollectionOptions.EnableW3CDistributedTracing), HttpAutoCollectionOptions.EnableW3CDistributedTracing },
                { nameof(HttpAutoCollectionOptions.EnableResponseHeaderInjection), HttpAutoCollectionOptions.EnableResponseHeaderInjection }
            };

            JObject options = new JObject
            {
                { nameof(SamplingSettings), sampling },
                { nameof(SnapshotConfiguration), snapshot },
                { nameof(EnablePerformanceCountersCollection), EnablePerformanceCountersCollection},
                { nameof(HttpAutoCollectionOptions), httpOptions},
                { nameof(LiveMetricsInitializationDelay), LiveMetricsInitializationDelay },
                { nameof(EnableLiveMetrics), EnableLiveMetrics },
                { nameof(EnableDependencyTracking), EnableDependencyTracking }
            };

            return options.ToString(Formatting.Indented);
        }
    }
}
