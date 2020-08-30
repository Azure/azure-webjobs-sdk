// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute that can be applied at the class or function level to set 
    /// executions retries of job functions.
    /// </summary>
    public class RetryAttribute : Attribute
    {
        /// <summary>
        /// Constructs a new instance with <see cref="FixedDelayStrategy"/>
        /// </summary>
        /// <param name="maxRetryCount">Set value for maximum number of retries per invocation if function invocation fails.</param>
        /// <param name="delayInterval">Delay between retries</param>
        public RetryAttribute(int maxRetryCount, string delayInterval)
        {
            MaxRetryCount = maxRetryCount;
            if (maxRetryCount < -1)
            {
                throw new InvalidOperationException("'retryCount' must be >= -1.");
            }
            DelayInterval = delayInterval;
        }

        /// <summary>
        /// Constructs a new instance with delay strategy <see cref="RandomizedExponentialBackoffStrategy"/>.
        /// </summary>
        /// <param name="maxRetryCount"></param>
        /// <param name="minimumInterval"></param>
        /// <param name="maximumInterval"></param>
        public RetryAttribute(int maxRetryCount, string minimumInterval, string maximumInterval)
        {
            MaxRetryCount = maxRetryCount;
            if (maxRetryCount < -1)
            {
                throw new InvalidOperationException("'retryCount' must be >= -1.");
            }
            MinimumInterval = minimumInterval;
            MaxmumInterval = maximumInterval;
        }

        public int MaxRetryCount { get; }

        public string DelayInterval { get; }

        public string MinimumInterval { get; }

        public string MaxmumInterval { get; }
    }
}
