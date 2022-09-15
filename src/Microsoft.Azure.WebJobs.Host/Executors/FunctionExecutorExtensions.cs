// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Logging;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    /// <summary>
    /// Provides extension methods for the <see cref="IFunctionExecutor"/> interface.
    /// </summary>
    internal static class FunctionExecutorExtensions
    {
        public static async Task<IDelayedException> TryExecuteAsync(this IFunctionExecutor executor, Func<IFunctionInstance> instanceFactory, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
        {
            var attempt = 0;
            IDelayedException functionResult = null;
            ILogger logger = null;
            RetryContext retryContext = null;
            bool isRetryPendingSet = false;
            IRetryNotifier retryNotifier = executor as IRetryNotifier;

            try
            {
                while (true)
                {
                    IFunctionInstance functionInstance = instanceFactory.Invoke();
                    if (logger == null)
                    {
                        logger = loggerFactory.CreateLogger(LogCategories.CreateFunctionCategory(functionInstance.FunctionDescriptor.LogName));
                    }

                    if (retryContext == null && functionInstance.FunctionDescriptor.RetryStrategy != null)
                    {
                        retryContext = new RetryContext();
                        retryContext.MaxRetryCount = functionInstance.FunctionDescriptor.RetryStrategy.MaxRetryCount;
                    }

                    if (retryContext != null && functionInstance is FunctionInstance instance)
                    {
                        retryContext.Instance = instance;
                        instance.RetryContext = retryContext;
                    }
                    functionResult = await executor.TryExecuteAsync(functionInstance, cancellationToken);

                    if (functionResult == null)
                    {
                        // function invocation succeeded
                        break;
                    }
                    if (functionInstance.FunctionDescriptor.RetryStrategy == null)
                    {
                        // retry is not configured
                        break;
                    }

                    IRetryStrategy retryStrategy = functionInstance.FunctionDescriptor.RetryStrategy;
                    if (retryStrategy.MaxRetryCount != -1 && attempt == retryStrategy.MaxRetryCount)
                    {
                        // retry count exceeded
                        logger.LogFunctionRetriesFailed(attempt, functionResult);
                        break;
                    }

                    retryContext.RetryCount = ++attempt;
                    retryContext.Exception = functionResult?.Exception;

                    TimeSpan nextDelay = retryStrategy.GetNextDelay(retryContext);
                    logger.LogFunctionRetryAttempt(nextDelay, attempt, retryStrategy.MaxRetryCount);

                    if (retryNotifier != null && !isRetryPendingSet)
                    {
                        isRetryPendingSet = true;
                        retryNotifier.RetryPending();
                    }

                    try
                    {
                        // If the invocation is cancelled retries must stop.
                        await Task.Delay(nextDelay, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        logger.LogExitFromRetryLoop();
                        break;
                    }
                }
            }
            finally
            {
                if (retryNotifier != null && isRetryPendingSet)
                {
                    retryNotifier.RetryComplete();
                }
            }

            return functionResult;
        }
    }
}
