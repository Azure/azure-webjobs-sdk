// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Storage
{
    internal class TimeoutScope : IDisposable
    {
        private readonly Func<HttpResponseMessage> _getResponse;
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly CancellationTokenRegistration _registration;

        public TimeoutScope(Func<HttpResponseMessage> getResponse, ILogger logger)
        {
            _getResponse = getResponse ?? throw new ArgumentNullException(nameof(getResponse));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _registration = _cts.Token.Register(DisposeResponse);
            _cts.CancelAfter(TimeSpan.FromMinutes(2));
        }

        private void DisposeResponse()
        {
            _logger.LogDebug("Timeout fired. Disposing Response.");
            _getResponse()?.Dispose();
        }

        public void Dispose()
        {
            _registration.Dispose();
            _cts.Dispose();
        }
    }
}
