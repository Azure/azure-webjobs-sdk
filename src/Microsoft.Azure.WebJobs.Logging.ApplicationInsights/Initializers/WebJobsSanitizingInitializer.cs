// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    internal class WebJobsSanitizingInitializer : ITelemetryInitializer
    {
        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry == null)
            {
                return;
            }

            if (telemetry is ISupportProperties propertyTelemetry)
            {
                foreach (KeyValuePair<string, string> property in propertyTelemetry.Properties)
                {
                    propertyTelemetry.Properties[property.Key] = Sanitizer.Sanitize(property.Value);
                }
            }

            if (telemetry is TraceTelemetry traceTelemetry)
            {
                traceTelemetry.Message = Sanitizer.Sanitize(traceTelemetry.Message);
            }
        }
    }
}
