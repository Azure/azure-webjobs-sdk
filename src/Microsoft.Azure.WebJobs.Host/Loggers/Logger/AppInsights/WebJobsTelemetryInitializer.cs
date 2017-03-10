// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace Microsoft.Azure.WebJobs.Host.Loggers
{
    internal class WebJobsTelemetryInitializer : ITelemetryInitializer
    {
        internal const string AzureWebsiteName = "WEBSITE_SITE_NAME";

        public void Initialize(ITelemetry telemetry)
        {
            // RoleName is the app name
            telemetry.Context.Cloud.RoleName = Environment.GetEnvironmentVariable(AzureWebsiteName) ?? "[Unknown]";
        }
    }
}
