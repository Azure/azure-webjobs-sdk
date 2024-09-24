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
        private ITelemetryProcessor _next;
        private bool _isSamplingEnabled = false;        

        public DelayedSamplingProcessor(ITelemetryProcessor next, ApplicationInsightsLoggerOptions options)
        {
            _next = next;
            _samplingProcessor = new AdaptiveSamplingTelemetryProcessor(options.SamplingSettings, null, next);

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
            if (_isSamplingEnabled)
            {
                // Forward to Adaptive Sampling processor
                _samplingProcessor.Process(item);
            }
            else
            {
                // Bypass sampling
                _next.Process(item);
            }
        }

        private void EnableSampling()
        {
            _isSamplingEnabled = true;
        }
    }
}
