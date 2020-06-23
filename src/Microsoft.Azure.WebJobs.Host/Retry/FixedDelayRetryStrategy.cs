// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Timers;
using System;

namespace Microsoft.Azure.WebJobs.Host
{
    internal class FixedDelayRetryStrategy : RetryStrategy
    {
        public FixedDelayRetryStrategy(int maxRetryCount, TimeSpan delayInterval)
            : base(maxRetryCount, new FixedDelayStrategy(delayInterval))
        {
        }
    }
}
