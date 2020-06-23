// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Timers;
using System;

namespace Microsoft.Azure.WebJobs.Host
{
    internal class ExponentialBackoffRetryStrategy : RetryStrategy
    {
        public ExponentialBackoffRetryStrategy(int maxRetryCount, TimeSpan minimumInterval, TimeSpan maximumInterval)
            : base(maxRetryCount, new RandomizedExponentialBackoffStrategy(minimumInterval, maximumInterval))
        {
        }
    }
}
