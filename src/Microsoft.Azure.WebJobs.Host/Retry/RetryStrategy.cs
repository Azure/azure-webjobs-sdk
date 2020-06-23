// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Timers;
using System;

namespace Microsoft.Azure.WebJobs.Host
{
    internal abstract class RetryStrategy : IRetryStrategy
    {
        protected readonly IDelayStrategy _delayStrategy;
        protected readonly int _maxRetryCount;

        public RetryStrategy(int maxRetryCount, IDelayStrategy delayStrategy)
        {
            _maxRetryCount = maxRetryCount;
            _delayStrategy = delayStrategy;
        }

        public TimeSpan GetNextDelay(int currentRetryCount)
        {
            if (_maxRetryCount == -1 || currentRetryCount < _maxRetryCount)
            {
                return _delayStrategy.GetNextDelay(false);
            }
            return TimeSpan.Zero;
        }
    }
}
