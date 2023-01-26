// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        public ApplicationInsightsLoggerProvider(
            TelemetryClient client,
            DiagnosticSource source,
            CancellationTokenSource cancellationTokenSource)
        {
            _client = client;
            _source = source;
            _cancellationTokenSource = cancellationTokenSource;
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
                        _cancellationTokenSource = _cancellationTokenSource ?? new CancellationTokenSource(new TimeSpan(hours: 0, minutes: 0, seconds: 2));
                        var task = _client.FlushAsync(_cancellationTokenSource.Token);

                        // Wait for the flush to complete or for 5 seconds to pass, whichever comes first. This method throws and AggregateException with a
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
                        WriteDiagnosticFlushingIssue($"Flushing failed. CancelationTokenSource.IsCancellationRequested: {_cancellationTokenSource.IsCancellationRequested}");
                    }
                    finally
                    {
                        _cancellationTokenSource.Dispose();
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
