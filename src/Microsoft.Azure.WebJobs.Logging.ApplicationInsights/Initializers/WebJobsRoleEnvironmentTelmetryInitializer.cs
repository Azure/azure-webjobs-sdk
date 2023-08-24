// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    // This class was taken largely from https://raw.githubusercontent.com/Microsoft/ApplicationInsights-dotnet-server/91016d62f3181e10d4cf589ef8fd64dadb6b54a2/Src/WindowsServer/WindowsServer.Shared/AzureWebAppRoleEnvironmentTelemetryInitializer.cs, 
    // but refactored so that it did not use WEBSITE_HOSTNAME, which is determined to be unreliable for functions during slot swaps.

    /// <summary>
    /// A telemetry initializer that will gather Azure Web App Role Environment context information.
    /// </summary>    
    internal class WebJobsRoleEnvironmentTelemetryInitializer : ITelemetryInitializer
    {
        internal const string AzureWebsiteName = "WEBSITE_SITE_NAME";
        internal const string AzureWebsiteSlotName = "WEBSITE_SLOT_NAME";
        internal const string AzureWebsiteCloudRoleName = "WEBSITE_CLOUD_ROLENAME";
        private const string DefaultProductionSlotName = "production";
        private const string WebAppSuffix = ".azurewebsites.net";

        private ConcurrentDictionary<string, string> _siteNodeNames = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes <see cref="ITelemetry" /> device context.
        /// </summary>
        /// <param name="telemetry">The telemetry to initialize.</param>
        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry == null)
            {
                return;
            }

            Lazy<string> siteSlotName = new Lazy<string>(() =>
            {
                // We cannot cache these values as the environment variables can change on the fly.
                return GetAzureWebsiteUniqueSlotName();
            });

            var websiteCloudRoleName = Environment.GetEnvironmentVariable(AzureWebsiteCloudRoleName);

            if (!string.IsNullOrEmpty(websiteCloudRoleName))
            {
                telemetry.Context.Cloud.RoleName = websiteCloudRoleName;
            }

            if (string.IsNullOrEmpty(telemetry.Context.Cloud.RoleName))
            {
                telemetry.Context.Cloud.RoleName = siteSlotName.Value;
            }

            var internalContext = telemetry.Context.GetInternalContext();
            if (string.IsNullOrEmpty(internalContext.NodeName) &&
                !string.IsNullOrEmpty(siteSlotName.Value))
            {
                internalContext.NodeName = _siteNodeNames.GetOrAdd(siteSlotName.Value, p =>
                {
                    // maintain previous behavior of node having the full url
                    return p += WebAppSuffix;
                });
            }
        }

        /// <summary>
        /// Gets a value that uniquely identifies the site and slot.
        /// </summary>
        private static string GetAzureWebsiteUniqueSlotName()
        {
            string name = Environment.GetEnvironmentVariable(AzureWebsiteName);
            string slotName = Environment.GetEnvironmentVariable(AzureWebsiteSlotName);

            if (!string.IsNullOrEmpty(slotName) &&
                !string.Equals(slotName, DefaultProductionSlotName, StringComparison.OrdinalIgnoreCase))
            {
                name += $"-{slotName}";
            }

            return name?.ToLowerInvariant();
        }
    }
}