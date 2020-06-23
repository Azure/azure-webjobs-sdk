// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute that can be applied at the class or function level to set 
    /// executions retries of job functions.
    /// </summary>
    public class RetryAttribute : Attribute
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="retryCount">Sets vavalue for retry count. Use -1 for infinite retries.</param>
        /// <param name="sleepDuration">The duration to wait for for a particular retry attempt as a <see cref="TimeSpan"/> string (e.g. "00:00:10").</param>
        /// <param name="exponentialBackoff">Sets a value indicating whether retry will use exponential backoff.</param>
        public RetryAttribute(int retryCount, string sleepDuration, bool exponentialBackoff = false)
        {
            RetryCount = retryCount;
            if (retryCount < -1)
            {
                throw new InvalidOperationException("'retryCount' must be >= -1.");
            }

            TimeSpan parsedTimespan = TimeSpan.Zero;
            if (!string.IsNullOrEmpty(sleepDuration) && TimeSpan.TryParse(sleepDuration, CultureInfo.InvariantCulture, out parsedTimespan))
            {
                SleepDuration = parsedTimespan;
            }
            else if (exponentialBackoff == false)
            {
                throw new InvalidOperationException($"Can't parse sleepDuration='{sleepDuration}', please the string in format '00:00:00'.");
            }
            ExponentialBackoff = exponentialBackoff;
        }

        /// <summary>
        /// Gets or sets vavalue for retry count. Use -1 for infinite retries.
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// The duration to wait for for a particular retry attempt as a <see cref="TimeSpan"/> string (e.g. "00:00:10").
        /// </summary>
        public TimeSpan SleepDuration { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the expbnential backoff is used.
        /// </summary>
        public bool ExponentialBackoff { get; set; }
    }
}
