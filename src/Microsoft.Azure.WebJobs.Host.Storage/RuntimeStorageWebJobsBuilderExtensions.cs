// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs;

namespace Microsoft.Extensions.Hosting
{
    public static class RuntimeStorageWebJobsBuilderExtensions
    {
        // WebJobs v1 Classic logging. Needed for dashboard.         
        [Obsolete("Dashboard is being deprecated. Use AppInsights.")]
        public static IWebJobsBuilder AddDashboardLogging(this IWebJobsBuilder builder)
        {
            builder.Services.AddDashboardLogging();

            return builder;
        }

        // Make the Runtime itself use storage for its internal operations. 
        // Uses v1 app settings, via a LegacyConfigSetup object. 
        public static IWebJobsBuilder AddAzureStorageCoreServices(this IWebJobsBuilder builder)
        {
            builder.Services.AddAzureStorageCoreServices();
            return builder;
        }
    }
}
