// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Enumeration of built-in retry strategies. <see cref="RetryOptions.Strategy"/>
    /// </summary>
    public enum RetryStrategy
    {
        /// <summary>
        /// Retry policy is set using <see cref="FixedDelayRetryAttribute"/>
        /// </summary>
        ExponentialBackoff = 0,

        /// <summary>
        /// Retry policy is set using <see cref="ExponentialBackoffRetryAttribute"/>
        /// </summary>
        FixedDelay = 1
    }
}
