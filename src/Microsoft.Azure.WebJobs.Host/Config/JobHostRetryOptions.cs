// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Configuration options for controlling function execution retry behavior.
    /// </summary>
    public class JobHostRetryOptions
    {
        /// <summary>
        /// The retry count.
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// The duration to wait for for a particular retry attemp
        /// </summary>
        public TimeSpan SleepDuration { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Gets a value indicating whether the expbnential backoff is used.
        /// </summary>
        public bool ExponentialBackoff { get; set; }

        internal RetryAttribute ToAttribute()
        {
            return new RetryAttribute(RetryCount, SleepDuration.ToString(), ExponentialBackoff);
        }
    }
}
