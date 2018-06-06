// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace SampleHost
{
    public static class SampleLoggingExtensions
    {
        public static IHostBuilder AddApplicationInsights(this IHostBuilder builder)
        {
            // If AppInsights is enabled, build up a LoggerFactory
            string instrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");
            if (!string.IsNullOrEmpty(instrumentationKey))
            {
                var filter = new LogCategoryFilter
                {
                    DefaultLevel = LogLevel.Debug
                };

                filter.CategoryLevels[LogCategories.Results] = LogLevel.Debug;
                filter.CategoryLevels[LogCategories.Aggregator] = LogLevel.Debug;

                builder.AddApplicationInsights(instrumentationKey, filter.Filter, null);
            }

            return builder;
        }
    }
}
