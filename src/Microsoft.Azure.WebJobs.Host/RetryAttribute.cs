// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute that can be applied at the class or function level to specify a retry
    /// strategy for failed function invocations.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public abstract class RetryAttribute : Attribute, IRetryStrategy
    {
        public RetryAttribute(int maxRetryCount)
        {
            if (maxRetryCount < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRetryCount));
            }
            MaxRetryCount = maxRetryCount;
        }

        /// <summary>
        /// Gets the maximum number of retries allowed.
        /// </summary>
        public int MaxRetryCount { get; }

        /// <summary>
        /// Returns delay between function invocation retries. This is when number of retries < MaxRetryCount
        /// </summary>
        /// <param name="context"><see cref="RetryContext"/>)</param>
        /// <returns></returns>
        public abstract TimeSpan GetNextDelay(RetryContext context);
    }
}
