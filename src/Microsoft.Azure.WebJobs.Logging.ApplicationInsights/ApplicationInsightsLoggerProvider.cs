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
        public const string SourceName = ApplicationInsightsDiagnosticConstants.ApplicationInsightsDiagnosticSourcePrefix + "ApplicationInsightsLoggerProvider";

        private CancellationTokenSource _cancellationTokenSource;
        private readonly TelemetryClient _client;
        private readonly ApplicationInsightsLoggerOptions _loggerOptions;
        private DiagnosticSource _source = new DiagnosticListener(SourceName);
        private bool _disposed;

        public ApplicationInsightsLoggerProvider(TelemetryClient client, IOptions<ApplicationInsightsLoggerOptions> loggerOptions)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _loggerOptions = loggerOptions?.Value ?? throw new ArgumentNullException(nameof(loggerOptions));
        }
            
        // Constructor for testing purposes only
        internal ApplicationInsightsLoggerProvider(
            TelemetryClient client,
            DiagnosticSource source)
        {
            _client = client;
            _source = source;
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
                        // Default timeout of 2 seconds
                        _cancellationTokenSource = new CancellationTokenSource(2000);
                        var task = _client.FlushAsync(_cancellationTokenSource.Token);

                        // Wait for the flush to complete or for 2 seconds to pass, whichever comes first. This method throws an AggregateException with a
                        // TaskCanceledException object in the InnerExceptions collection if the flush is canceled
                        task.Wait();
                        
                        // Flush did not fully succeed
                        if (!task.Result)
                        {
                            WriteDiagnosticFlushingIssue("Flushing did not complete successfully");
                        }
                    }
                    catch (Exception)
                    {
                        WriteDiagnosticFlushingIssue($"Flushing failed. CancellationTokenSource.IsCancellationRequested: {_cancellationTokenSource.IsCancellationRequested}");
                    }
                    finally
                    {
                        _cancellationTokenSource.Dispose();
                        (_source as IDisposable)?.Dispose();
                    }
                }

                _disposed = true;
            }
        }

        private void WriteDiagnosticFlushingIssue(object value)
        {
            // We must include this check, otherwise the payload will be created even though nothing is listening
            // for the data: see https://github.com/dotnet/runtime/blob/main/src/libraries/System.Diagnostics.DiagnosticSource/src/DiagnosticSourceUsersGuide.md
            if (_source.IsEnabled(SourceName))
            {
                _source.Write(SourceName, value);
            }
        }
    }
}
