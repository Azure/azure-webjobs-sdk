// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Loggers
{
    internal class AppInsightsLoggerProvider : ILoggerProvider
    {
        private readonly TelemetryClient _client;
        private readonly Func<string, LogLevel, bool> _filter;

        public AppInsightsLoggerProvider(string instrumentationKey, Func<string, LogLevel, bool> filter)
        {
            if (instrumentationKey == null)
            {
                throw new ArgumentNullException(nameof(instrumentationKey));
            }

            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            // Add the initializer, which adds the app name to all telemetry
            TelemetryConfiguration.Active.TelemetryInitializers.Add(new WebJobsTelemetryInitializer());

            _client = new TelemetryClient();
            _client.InstrumentationKey = instrumentationKey;
            _filter = filter;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new AppInsightsLogger(_client, categoryName, _filter);
        }

        public void Dispose()
        {
        }
    }
}
