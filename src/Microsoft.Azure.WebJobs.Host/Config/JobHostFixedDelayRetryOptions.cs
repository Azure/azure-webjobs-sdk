// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs
{
    public class JobHostFixedDelayRetryOptions : JobHostRetryOptions
    {
        public TimeSpan Delay;

        internal override RetryAttribute ToAttribute()
        {
            return new RetryAttribute(MaxRetryCount, Delay.ToString());
        }
    }
}
