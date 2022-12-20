﻿// Copyright (c) .NET Foundation. All rights reserved.
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
        public const string ApplicationInsightsFlushingIssues = "ApplicationInsightsFlushingIssues";

        private readonly TelemetryClient _client;
        private readonly ApplicationInsightsLoggerOptions _loggerOptions;
        private DiagnosticListener _source = new DiagnosticListener(ApplicationInsightsFlushingIssues);
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
                        // Default timeout of 5 seconds
                        var timeout = new TimeSpan(hours: 0, minutes: 0, seconds: 5);
                        var cancellationTokenSource = new CancellationTokenSource();
                        var task = _client.FlushAsync(cancellationTokenSource.Token);

                        // Wait for the flush to complete or for 5 seconds to pass, whichever comes first
                        if (!task.Wait(timeout))
                        {
                            cancellationTokenSource.Cancel();
                            WriteDiagnosticFlushingIssue($"Flushing did not complete within {timeout.Seconds}s timeout");
                        }
                        // Flush did not complete successfully
                        else if (!task.Result)
                        {
                            WriteDiagnosticFlushingIssue("Flushing did not complete successfully");
                        }
                    }
                    catch (Exception e)
                    {
                        // Log flushing exceptions
                        WriteDiagnosticFlushingIssue(e.Message);
                    }
                }

                _disposed = true;
            }
        }

        private void WriteDiagnosticFlushingIssue(object value)
        {
            // We must include this check, otherwise the payload will be created even though nothing is listening
            // for the data: see https://github.com/dotnet/runtime/blob/main/src/libraries/System.Diagnostics.DiagnosticSource/src/DiagnosticSourceUsersGuide.md
            if (_source.IsEnabled(ApplicationInsightsFlushingIssues))
            {
                _source.Write(ApplicationInsightsFlushingIssues, value);
            }
        }
    }
}
