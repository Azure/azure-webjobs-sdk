// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.SnapshotCollector;
using Microsoft.ApplicationInsights.WindowsServer.Channel.Implementation;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    public class ApplicationInsightsLoggerOptions
    {
        public string InstrumentationKey { get; set; }

        public SamplingPercentageEstimatorSettings SamplingSettings { get; set; }

        public SnapshotCollectorConfiguration SnapshotConfiguration { get; set; }
    }
}
