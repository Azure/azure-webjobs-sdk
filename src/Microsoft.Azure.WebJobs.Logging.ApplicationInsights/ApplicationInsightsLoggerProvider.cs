﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    public class ApplicationInsightsLoggerProvider : ILoggerProvider
    {
        private readonly TelemetryClient _client;
        private bool _disposed;

        public ApplicationInsightsLoggerProvider(TelemetryClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public ILogger CreateLogger(string categoryName) => new ApplicationInsightsLogger(_client, categoryName);

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_client != null)
                {
                    try
                    {
                        _client.Flush();

                        // This sleep isn't ideal, but the flush is async so it's the best we have right now. This is
                        // being tracked at https://github.com/Microsoft/ApplicationInsights-dotnet/issues/407
                        Thread.Sleep(2000);
                    }
                    catch
                    {
                        // Ignore failures on dispose
                    }
                }

                _disposed = true;
            }
        }
    }
}
