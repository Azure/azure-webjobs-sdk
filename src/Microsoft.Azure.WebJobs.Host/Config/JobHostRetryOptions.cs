// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Configuration options for controlling function execution retry behavior.
    /// </summary>
    public abstract class JobHostRetryOptions
    {
        /// <summary>
        /// The maximum number of retries per invocation if function invocation fails.
        /// </summary>
        public int MaxRetryCount { get; set; }

        internal abstract RetryAttribute ToAttribute();
    }
}
