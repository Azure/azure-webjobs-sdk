// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using System;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    internal class MetricSdkVersionTelemetryInitializer : ITelemetryInitializer
    {
        private const string Prefix = "af_";
        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry == null)
            {
                return;
            }
            
            if (telemetry is MetricTelemetry)
            {
                var internalContext = telemetry.Context?.GetInternalContext();
                if (internalContext != null && internalContext.SdkVersion != null && !internalContext.SdkVersion.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                {
                    internalContext.SdkVersion = Prefix + internalContext.SdkVersion;
                }
            }
        }
    }
}