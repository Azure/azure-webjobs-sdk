// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host.Config
{
    public class RetryOptions
    {
        public RetryStrategy Strategy { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of retries allowed per function execution
        /// </summary>
        public int? MaxRetryCount { get; set; }

        /// <summary>
        /// Gets or sets the delay that will be used between retries when using <see cref="RetryStrategy.FixedDelay"/> strategy
        /// </summary>
        public TimeSpan? DelayInterval { get; set; }

        /// <summary>
        /// Gets or sets the minimum retry delay when using <see cref="RetryStrategy.ExponentialBackoff"/> strategy
        /// </summary>
        public TimeSpan? MinimumInterval { get; set; }

        /// <summary>
        /// Gets or sets the maximum retry delay when using <see cref="RetryStrategy.ExponentialBackoff"/> strategy
        /// </summary>
        public TimeSpan? MaximumInterval { get; set; }

        /// <summary>
        /// Converts to <see cref="RetryAttribute"/> attribute
        /// </summary>
        /// <returns></returns>
        public RetryAttribute ToAttribute()
        {
            switch (Strategy)
            {
                case RetryStrategy.FixedDelay:
                    if (MaxRetryCount.HasValue && DelayInterval.HasValue)
                    {
                        return new FixedDelayRetryAttribute(MaxRetryCount.Value, DelayInterval.ToString());
                    }
                    break;
                case RetryStrategy.ExponentialBackoff:
                    if (MaxRetryCount.HasValue && MinimumInterval.HasValue && MaximumInterval.HasValue)
                    {
                        return new ExponentialBackoffRetryAttribute(MaxRetryCount.Value, MinimumInterval.ToString(), MaximumInterval.ToString());
                    }
                    break;
            }
            return null;
        }
    }
}
