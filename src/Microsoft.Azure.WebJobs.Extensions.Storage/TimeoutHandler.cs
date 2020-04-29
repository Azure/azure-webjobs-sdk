// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Storage
{
    internal static class TimeoutHandler
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

        public static async Task<T> ExecuteWithTimeout<T>(string operationName, string clientRequestId, ILogger logger, Func<Task<T>> operation)
        {
            using (WebJobsStorageDelegatingHandler.CreateTimeoutScope(logger))
            {
                T result = default(T);

                try
                {
                    result = await operation();
                }
                catch (OperationCanceledException)
                {
                    logger.LogDebug($"The operation '{operationName}' with id '{clientRequestId}' did not complete in '{DefaultTimeout}'.");
                }

                return result;
            }
        }
    }
}
