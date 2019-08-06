// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Timers;

namespace Microsoft.Azure.WebJobs.Extensions.Storage
{
    internal static class TimeoutHandler
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

        public static async Task<T> ExecuteWithTimeout<T>(string operationName, string clientRequestId, IWebJobsExceptionHandler exceptionHandler, Func<Task<T>> operation)
        {
            using (var cts = new CancellationTokenSource())
            {
                Task timeoutTask = Task.Delay(DefaultTimeout, cts.Token);
                Task<T> operationTask = operation();

                Task completedTask = await Task.WhenAny(timeoutTask, operationTask);

                if (Equals(timeoutTask, completedTask))
                {
                    ExceptionDispatchInfo exceptionDispatchInfo;
                    try
                    {
                        throw new TimeoutException($"The operation '{operationName}' with id '{clientRequestId}' did not complete in '{DefaultTimeout}'.");
                    }
                    catch (TimeoutException ex)
                    {
                        exceptionDispatchInfo = ExceptionDispatchInfo.Capture(ex);
                    }

                    await exceptionHandler.OnUnhandledExceptionAsync(exceptionDispatchInfo);

                    return default(T);
                }

                // Cancel the Delay.
                cts.Cancel();

                return await operationTask;
            }
        }
    }
}
