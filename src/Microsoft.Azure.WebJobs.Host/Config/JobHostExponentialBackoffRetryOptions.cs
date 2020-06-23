// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs
{
    public class JobHostExponentialBackoffRetryOptions : JobHostRetryOptions
    {
        public TimeSpan MinimumDelay;
        public TimeSpan MaximumDelay;

        internal override RetryAttribute ToAttribute()
        {
            return new RetryAttribute(MaxRetryCount, MinimumDelay.ToString(), MaximumDelay.ToString());
        }
    }
}
