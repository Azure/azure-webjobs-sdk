// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;


namespace Microsoft.Azure.WebJobs.Host
{
    internal class RetryStrategyProvider
    {
        internal IRetryStrategy Create(RetryAttribute retryAttribute)
        {
            if (retryAttribute == null)
            {
                return null;
            }
            if (retryAttribute.MaxRetryCount == 0)
            {
                return null;
            }
            if (!string.IsNullOrEmpty(retryAttribute.DelayInterval))
            {
                return new FixedDelayRetryStrategy(retryAttribute.MaxRetryCount, TimeSpan.Parse(retryAttribute.DelayInterval));
            }
            return new ExponentialBackoffRetryStrategy(retryAttribute.MaxRetryCount, TimeSpan.Parse(retryAttribute.MinimumInterval), TimeSpan.Parse(retryAttribute.MinimumInterval));
        }
    }
}
