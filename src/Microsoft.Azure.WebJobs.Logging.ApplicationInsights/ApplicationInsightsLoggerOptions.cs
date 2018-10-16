// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.SnapshotCollector;
using Microsoft.ApplicationInsights.WindowsServer.Channel.Implementation;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    public class ApplicationInsightsLoggerOptions
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
        public string QuickPulseAuthenticationApiKey { get; set; }

        /// <summary>
        /// Gets or sets flag that enables support of W3C distributed tracing protocol
        /// (and turns on legacy correlation schema).  Enabled by default.
        /// </summary>
        public bool EnableW3CDistributedTracing { get; set; } = true;

        /// <summary>
        /// Gets or sets a flag that enables injection of multi-component correlation headers into responses.
        /// This allows Application Insights to construct an Application Map to  when several
        /// instrumentation keys are used.  Disabled by default.
        /// </summary>
        public bool EnableResponseHeaderInjection { get; set; } = true;
    }
}
