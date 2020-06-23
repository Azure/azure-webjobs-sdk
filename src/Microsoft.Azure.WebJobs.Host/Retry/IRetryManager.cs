// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Defines an interface for the execution with retries.
    /// </summary>
    public interface IRetryManager
    {
        /// <summary>
        /// Executes a functoin with retries.
        /// </summary>
        /// <param name="executeFunc">The function execution action to perform.</param>
        /// <param name="input">Input value for a triggered function execution.</param>
        /// <returns>Returns functions execution result.</returns>
        Task<FunctionResult> ExecuteWithRetriesAsync(Func<TriggeredFunctionData, CancellationToken, Task<FunctionResult>> executeFunc, TriggeredFunctionData input, CancellationToken token);
    }
}
