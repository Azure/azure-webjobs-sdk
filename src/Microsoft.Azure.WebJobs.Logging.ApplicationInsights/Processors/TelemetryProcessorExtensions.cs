// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    internal static class TelemetryProcessorFactory
    {
        internal static AdaptiveSamplingTelemetryProcessor CreateAdaptiveSamplingProcessor(ApplicationInsightsLoggerOptions options, ITelemetryProcessor next = null)
        {
            // Create the sampling processor
            var samplingProcessor = new AdaptiveSamplingTelemetryProcessor(options.SamplingSettings, null, next);

            if (options.SamplingExcludedTypes != null)
            {
                samplingProcessor.ExcludedTypes = options.SamplingExcludedTypes;
            }
            if (options.SamplingIncludedTypes != null)
            {
                samplingProcessor.IncludedTypes = options.SamplingIncludedTypes;
            }
            return samplingProcessor;
        }
    }
}
