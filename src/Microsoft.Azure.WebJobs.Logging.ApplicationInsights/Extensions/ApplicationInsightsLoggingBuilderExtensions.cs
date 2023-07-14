// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// Extensions for ApplicationInsights configuration on an <see cref="ILoggingBuilder"/>. 
    /// </summary>
    public static class ApplicationInsightsLoggingBuilderExtensions
    {
        [Obsolete("Use " + nameof(AddApplicationInsightsWebJobs) + " instead.", false)]
        public static ILoggingBuilder AddApplicationInsights(
            this ILoggingBuilder builder)
        {
            return AddApplicationInsightsWebJobs(builder);
        }

        [Obsolete("Use " + nameof(AddApplicationInsightsWebJobs) + " instead.", false)]
        public static ILoggingBuilder AddApplicationInsights(
           this ILoggingBuilder builder,
           Action<ApplicationInsightsLoggerOptions> configure)
        {
            return AddApplicationInsightsWebJobs(builder, configure);
        }

        /// <summary>
        /// Registers Application Insights and <see cref="ApplicationInsightsLoggerProvider"/> with an <see cref="ILoggingBuilder"/>.
        /// </summary>        
        public static ILoggingBuilder AddApplicationInsightsWebJobs(
            this ILoggingBuilder builder)
        {
            return builder.AddApplicationInsightsWebJobs(null);
        }

        /// <summary>
        /// Registers Application Insights and <see cref="ApplicationInsightsLoggerProvider"/> with an <see cref="ILoggingBuilder"/>.
        /// </summary>        
        public static ILoggingBuilder AddApplicationInsightsWebJobs(
             this ILoggingBuilder builder,
             Action<ApplicationInsightsLoggerOptions> loggerOptionsConfiguration)
        {
            return builder.AddApplicationInsightsWebJobs(loggerOptionsConfiguration, null);
        }

        /// <summary>
        /// Registers Application Insights and <see cref="ApplicationInsightsLoggerProvider"/> with an <see cref="ILoggingBuilder"/>.
        /// </summary>  
        public static ILoggingBuilder AddApplicationInsightsWebJobs(
            this ILoggingBuilder builder,
            Action<ApplicationInsightsLoggerOptions> loggerOptionsConfiguration,
            Action<TelemetryConfiguration> additionalTelemetryConfiguration)
        {
            builder.AddConfiguration();
            builder.Services.AddApplicationInsights(loggerOptionsConfiguration, additionalTelemetryConfiguration);

            builder.Services.AddOptions<LoggerFilterOptions>()
                .PostConfigure<IOptions<ApplicationInsightsLoggerOptions>>((o, appInsightsOptions) =>
                {
                // The custom filtering below is only needed if we are sending all logs to Live Metrics.
                // If we are filtering (or not using Live Metrics) we don't need to do this.
                if (appInsightsOptions != null)
                {
                    if (!appInsightsOptions.Value.EnableLiveMetrics ||
                        appInsightsOptions.Value.EnableLiveMetricsFilters)
                    {
                        return;
                    }
                }

                // We want all logs to flow through the logger so they show up in QuickPulse.
                // To do that, we'll hide all registered rules inside of this one. They will be re-populated
                // and used by the FilteringTelemetryProcessor further down the pipeline.
                string fullTypeName = typeof(ApplicationInsightsLoggerProvider).FullName;
                IList<LoggerFilterRule> matchingRules = o.Rules.Where(r =>
                {
                    return r.ProviderName == fullTypeName
                        || r.ProviderName == ApplicationInsightsLoggerProvider.Alias;
                }).ToList();

                foreach (var rule in matchingRules)
                {
                    o.Rules.Remove(rule);
                }

                o.Rules.Add(new ApplicationInsightsLoggerFilterRule(matchingRules));
            });

            return builder;
        }
    }
}