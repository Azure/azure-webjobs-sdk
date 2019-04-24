// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    internal class PerfCounterSdkVersionTelemetryInitializer : ITelemetryInitializer
    {
        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry == null)
            {
                return;
            }
            
            if (telemetry is PerformanceCounterTelemetry)
            {
                var internalContext = telemetry.Context != null ? telemetry.Context.GetInternalContext() : null;
                if (internalContext != null && internalContext.SdkVersion != null)
                {
                    internalContext.SdkVersion = "f_" + telemetry.Context.GetInternalContext().SdkVersion;
                }
            }
        }
    }
}