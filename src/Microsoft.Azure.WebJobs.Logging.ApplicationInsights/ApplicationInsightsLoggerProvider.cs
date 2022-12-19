// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    [ProviderAlias(Alias)]
    public class ApplicationInsightsLoggerProvider : ILoggerProvider
    {
        internal const string Alias = "ApplicationInsights";
        // Allow for subscribing to flushing exceptions
        public const string ApplicationInsightsFlushingExceptions = "ApplicationInsightsFlushingExceptions";

        private readonly TelemetryClient _client;
        private readonly ApplicationInsightsLoggerOptions _loggerOptions;
        private DiagnosticListener _source = new DiagnosticListener(ApplicationInsightsFlushingExceptions);
        private bool _disposed;

        public ApplicationInsightsLoggerProvider(TelemetryClient client, IOptions<ApplicationInsightsLoggerOptions> loggerOptions)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _loggerOptions = loggerOptions?.Value ?? throw new ArgumentNullException(nameof(loggerOptions));
        }

        public ILogger CreateLogger(string categoryName) => new ApplicationInsightsLogger(_client, categoryName, _loggerOptions);

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_client != null)
                {
                    try
                    {
                        // Flushing logs upon disposal should not be canceled
                        var task = _client.FlushAsync(CancellationToken.None);
                        task.Wait();
                    }
                    catch (Exception e)
                    {
                        // Log flushing exceptions
                        if (_source.IsEnabled(ApplicationInsightsFlushingExceptions))
                        {
                            _source.Write(ApplicationInsightsFlushingExceptions, e.Message);
                        }
                    }
                }

                _disposed = true;
            }
        }
    }
}
