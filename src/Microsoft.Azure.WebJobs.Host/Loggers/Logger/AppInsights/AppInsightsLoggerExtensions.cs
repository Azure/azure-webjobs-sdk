// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Loggers;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// Extensions for adding the <see cref="AppInsightsLoggerProvider"/> to an <see cref="ILoggerFactory"/>. 
    /// </summary>
    public static class AppInsightsLoggerExtensions
    {
        /// <summary>
        /// Registers an <see cref="AppInsightsLoggerProvider"/> with an <see cref="ILoggerFactory"/>.
        /// </summary>
        /// <param name="factory">The factory.</param>
        /// <param name="instrumentationKey">The Application Insights instrumentation key.</param>
        /// <param name="filter">A filter that returns true if a message with the specified <see cref="LogLevel"/>
        /// and category should be logged. You can use <see cref="LoggerConfiguration.DefaultFilter(string, LogLevel)"/>
        /// or write a custom filter.</param>
        /// <returns>A <see cref="ILoggerFactory"/> for chaining additional operations.</returns>
        public static ILoggerFactory AddAppInsights(
            this ILoggerFactory factory,
            string instrumentationKey,
            Func<string, LogLevel, bool> filter)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (instrumentationKey == null)
            {
                throw new ArgumentNullException(nameof(instrumentationKey));
            }

            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            factory.AddProvider(new AppInsightsLoggerProvider(instrumentationKey, filter));

            return factory;
        }
    }
}
