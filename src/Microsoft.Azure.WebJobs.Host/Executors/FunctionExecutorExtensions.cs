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
    /// <summary>Provides extension methods for the <see cref="IFunctionExecutor"/> interface.</summary>
    internal static class FunctionExecutorExtensions
    {
        public static async Task<IDelayedException> TryExecuteAsync(this IFunctionExecutor executor, Func<IFunctionInstance> instanceFactory, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
        {
            var attempt = 0;
            IDelayedException functionResult = null;
            ILogger logger = null;
            while (true)
            {
                IFunctionInstance functionInstance = instanceFactory.Invoke();
                if (logger == null)
                {
                    logger = loggerFactory.CreateLogger(LogCategories.CreateFunctionCategory(functionInstance.FunctionDescriptor.LogName));
                }
                functionResult = await executor.TryExecuteAsync(functionInstance, cancellationToken);
                if (functionResult == null || functionInstance.FunctionDescriptor.RetryStrategy == null)
                {
                    // function invocation succeeded 
                    break;
                }

                IRetryStrategy retryStrategy = functionInstance.FunctionDescriptor.RetryStrategy;
                if (retryStrategy.MaxRetryCount != -1 && ++attempt > retryStrategy.MaxRetryCount)
                {
                    // no.of retries exceeded
                    break;
                }

                // Build retry context
                var retryContext = new RetryContext
                {
                    RetryCount = attempt,
                    Exception = functionResult.Exception,
                    Instance = functionInstance
                };

                TimeSpan nextDelay = retryStrategy.GetNextDelay(retryContext);
                logger.LogFunctionRetryAttempt(nextDelay, attempt, retryStrategy.MaxRetryCount);
                await Task.Delay(nextDelay);
            }
            return functionResult;
        }
    }
}
