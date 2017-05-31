// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    /// <summary>
    /// Interface defining the contract for executing a triggered function.
    /// Allows a hook around the underlying execution.
    /// This should only be used by extensions that need very specific control over the invocation. 
    /// </summary>
    public interface ITriggeredFunctionExecutorWithHook
    {
        /// <summary>
        /// Try to invoke the triggered function using the values specified.
        /// </summary>
        /// <param name="input">The trigger invocation details.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <param name="hook">a hook that wraps the underlying invocation</param>
        /// <returns>A <see cref="FunctionResult"/> describing the results of the invocation.</returns>
        Task<FunctionResult> TryExecuteAsync(TriggeredFunctionData input, CancellationToken cancellationToken, Func<Func<Task>, Task> hook);
    }
}
