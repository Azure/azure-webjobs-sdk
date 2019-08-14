// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Configuration;

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
            Action<ApplicationInsightsLoggerOptions> configure)
        {
            builder.AddConfiguration();
            builder.Services.AddApplicationInsights(configure);

            builder.Services.PostConfigure<LoggerFilterOptions>(o =>
            {
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