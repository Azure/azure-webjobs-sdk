// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Storage
{
    internal class WebJobsStorageDelegatingHandler : DelegatingHandler
    {
        private static readonly AsyncLocal<HttpResponseMessage> _capturedResponse = new AsyncLocal<HttpResponseMessage>();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return base.SendAsync(request, cancellationToken)
                .ContinueWith(t => _capturedResponse.Value = t.Result, TaskContinuationOptions.ExecuteSynchronously);
        }

        internal static IDisposable CreateTimeoutScope(ILogger logger)
        {
            return new TimeoutScope(() => _capturedResponse.Value, logger);
        }
    }
}
