// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Timers;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Defines a retry strategy where a fixed delay is used between retries.
    /// </summary>
    public sealed class FixedDelayRetryAttribute : RetryAttribute
    {
        private IDelayStrategy _delayStrategy;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="maxRetryCount">The maximum number of retries that will be attempted.</param>
        /// <param name="delayInterval">The delay between retries.</param>
        public FixedDelayRetryAttribute(int maxRetryCount, string delayInterval) : base(maxRetryCount)
        {
            if (!TimeSpan.TryParse(delayInterval, out TimeSpan parsedDelayInterval))
            {
                throw new ArgumentOutOfRangeException(nameof(delayInterval));
            }
            DelayInterval = delayInterval;

            _delayStrategy = new FixedDelayStrategy(parsedDelayInterval);
        }

        /// <summary>
        /// Gets the delay that will be used between retries.
        /// </summary>
        public string DelayInterval { get; }

        public override TimeSpan GetNextDelay(RetryContext context)
        {
            if (MaxRetryCount == -1 || context.RetryCount <= MaxRetryCount)
            {
                return _delayStrategy.GetNextDelay(false);
            }
            return TimeSpan.Zero;
        }
    }
}
