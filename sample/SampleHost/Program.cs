// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Extensions.Logging;

namespace SampleHost
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = new JobHostConfiguration();
            config.Queues.VisibilityTimeout = TimeSpan.FromSeconds(15);
            config.Queues.MaxDequeueCount = 3;

            if (config.IsDevelopment)
            {
                config.UseDevelopmentSettings();
            }

            // Build up a LoggerFactory to log to App Insights, but only if this key exists.
            string instrumentationKey = Environment.GetEnvironmentVariable("ApplicationInsightsInstrumentationKey");
            if (!string.IsNullOrEmpty(instrumentationKey))
            {
                // build up log levels for any category
                FilterBuilder filterBuilder = new FilterBuilder();
                filterBuilder.DefaultLevel = LogLevel.Error;
                filterBuilder.CategoryFilters[LoggingCategories.Function] = LogLevel.Information;
                filterBuilder.CategoryFilters[LoggingCategories.Results] = LogLevel.Information;
                filterBuilder.CategoryFilters[LoggingCategories.Aggregator] = LogLevel.Information;

                ILoggerFactory factory = new LoggerFactory()
                    .AddAppInsights(instrumentationKey, filterBuilder.Filter)
                    .AddConsole(filterBuilder.Filter);

                config.AddService(factory);
            }

            var host = new JobHost(config);
            host.RunAndBlock();
        }
    }
}
