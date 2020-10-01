// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    /// <summary>Provides extension methods for the <see cref="IFunctionExecutor"/> interface.</summary>
    public static class FunctionExecutorExtensions
    {
        internal static async Task<IDelayedException> TryExecuteAsync(this IFunctionExecutor executor, Func<IFunctionInstance> instanceFactory, IRetryStrategy retryStrategy, ILogger logger, CancellationToken cancellationToken)
        {
            var attempt = 0;
            IDelayedException functionResult = null;
            bool retriesExceeded = false;
            while (!retriesExceeded)
            {
                IFunctionInstance functionInstance = instanceFactory.Invoke();
                functionResult = await executor.TryExecuteAsync(functionInstance, cancellationToken);
                retriesExceeded = await Utility.WaitForNextExecutionAttempt(functionInstance, functionResult, retryStrategy, logger, attempt);
                if (retriesExceeded)
                {
                    break;
                }
                attempt++;
            }
            return functionResult;
        }
    }
}
