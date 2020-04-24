// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Storage
{
    internal static class TimeoutHandler
    {
        private static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan DeadlockTimeout = TimeSpan.FromMinutes(3);
        private static readonly string Version = typeof(TimeoutHandler).GetType().Assembly.GetName().Version.ToString();

        public static async Task<T> ExecuteWithTimeout<T>(string operationName, string clientRequestId, IWebJobsExceptionHandler exceptionHandler, ILogger logger, Func<CancellationToken, Task<T>> operation)
        {
            using (var operationCts = new CancellationTokenSource())
            {
                using (var deadlockCts = new CancellationTokenSource())
                {
                    operationCts.CancelAfter(OperationTimeout);

                    try
                    {
                        Task timeoutTask = Task.Delay(DeadlockTimeout, deadlockCts.Token);
                        Task<T> operationTask = operation(operationCts.Token);

                        Task completedTask = await Task.WhenAny(timeoutTask, operationTask);

                        if (Equals(timeoutTask, completedTask))
                        {
                            // Just in case the operation cancellation doesn't work, cancel the whole thing and restart.
                            await HandleDeadlockExceptionAsync(operationName, clientRequestId, exceptionHandler);

                            return default(T);
                        }

                        return await operationTask;
                    }
                    catch (OperationCanceledException)
                    {
                        // Make sure it was this token that was canceled.
                        if (operationCts.IsCancellationRequested)
                        {
                            logger.LogDebug($"The operation '{operationName}' with id '{clientRequestId}' was canceled after '{OperationTimeout}'. Version: '{Version}'");

                            return default(T);
                        }
                        else
                        {
                            throw;
                        }
                    }
                    finally
                    {
                        deadlockCts.Cancel();
                    }
                }
            }
        }

        private static Task HandleDeadlockExceptionAsync(string operationName, string clientRequestId, IWebJobsExceptionHandler exceptionHandler)
        {
            ExceptionDispatchInfo exceptionDispatchInfo;
            try
            {
                throw new TimeoutException($"The operation '{operationName}' with id '{clientRequestId}' did not complete in '{DeadlockTimeout}'. Version: '{Version}'");
            }
            catch (TimeoutException ex)
            {
                exceptionDispatchInfo = ExceptionDispatchInfo.Capture(ex);
            }

            return exceptionHandler.OnUnhandledExceptionAsync(exceptionDispatchInfo);
        }
    }
}
