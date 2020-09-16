// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Defines a retry delay strategy for failed function invocations.
    /// </summary>
    public interface IRetryStrategy
    {
        /// <summary>
        /// Gets the maximum number of retries allowed.
        /// </summary>
        int MaxRetryCount { get; }

        /// <summary>
        /// Gets the next delay that should be used before the next retry.
        /// </summary>
        /// <param name="context">Context for the failed invocation.</param>
        /// <returns>A <see cref="TimeSpan"/> representing the delay.</returns>
        TimeSpan GetNextDelay(RetryContext context);
    }
}
