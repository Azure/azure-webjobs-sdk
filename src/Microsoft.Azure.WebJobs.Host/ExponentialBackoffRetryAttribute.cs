// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Timers;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Defines an exponential backoff retry strategy, where the delay between retries
    /// will get progressively larger, limited by the max/min specified.
    /// </summary>
    public class ExponentialBackoffRetryAttribute : RetryAttribute
    {
        private IDelayStrategy _delayStrategy;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="maxRetryCount">The maximum number of retries that will be attempted.</param>
        /// <param name="minimumInterval">The minimum delay interval.</param>
        /// <param name="maximumInterval">The maximum delay interval.</param>
        public ExponentialBackoffRetryAttribute(int maxRetryCount, string minimumInterval, string maximumInterval) : base(maxRetryCount)
        {
            if (!TimeSpan.TryParse(minimumInterval, out TimeSpan parsedMinimumInterval))
            {
                throw new ArgumentOutOfRangeException(nameof(minimumInterval));
            }
            if (!TimeSpan.TryParse(maximumInterval, out TimeSpan parsedMaximumInterval))
            {
                throw new ArgumentOutOfRangeException(nameof(maximumInterval));
            }
            MinimumInterval = minimumInterval;
            MaxmumInterval = maximumInterval;

            _delayStrategy = new RandomizedExponentialBackoffStrategy(parsedMinimumInterval, parsedMaximumInterval);
        }

        /// <summary>
        /// Gets the minimum retry delay.
        /// </summary>
        public string MinimumInterval { get; }

        /// <summary>
        /// Gets the maximum retry delay.
        /// </summary>
        public string MaxmumInterval { get; }

        public override TimeSpan GetNextDelay(RetryContext context)
        {
            if (MaxRetryCount == -1 || context.RetryCount < MaxRetryCount)
            {
                return _delayStrategy.GetNextDelay(false);
            }
            return TimeSpan.Zero;
        }
    }
}
