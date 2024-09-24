// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    internal class DelayedSamplingProcessor : ITelemetryProcessor
    {
        private readonly AdaptiveSamplingTelemetryProcessor _samplingProcessor;
        private ITelemetryProcessor next;
        private bool isSamplingEnabled = false;        

        public DelayedSamplingProcessor(ITelemetryProcessor next, ApplicationInsightsLoggerOptions options, AdaptiveSamplingTelemetryProcessor samplingProcessor = null)
        {
            this.next = next;

            _samplingProcessor = samplingProcessor ?? new AdaptiveSamplingTelemetryProcessor(options.SamplingSettings, null, next);

            if (options.SamplingExcludedTypes != null)
            {
                _samplingProcessor.ExcludedTypes = options.SamplingExcludedTypes;
            }
            if (options.SamplingIncludedTypes != null)
            {
                _samplingProcessor.IncludedTypes = options.SamplingIncludedTypes;
            }

            // Start a timer to enable sampling after a delay        
            Task.Delay(options.AdaptiveSamplingInitializationDelay).ContinueWith(t => EnableSampling());
        }

        public void Process(ITelemetry item)
        {
            if (isSamplingEnabled)
            {
                // Forward to Adaptive Sampling processor
                _samplingProcessor.Process(item);
            }
            else
            {
                // Bypass sampling
                next.Process(item);
            }
        }

        private void EnableSampling()
        {
            isSamplingEnabled = true;
        }
    }
}
