// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Provides context for a failed function invocation retry. See <see cref="IRetryStrategy.GetNextDelay(RetryContext)."/>
    /// </summary>
    public class RetryContext
    {
        /// <summary>
        /// Gets or sets the current retry count.
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Gets or sets the max retry count.
        /// </summary>
        public int MaxRetryCount { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Exception"/> for the failed invocation.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="IFunctionInstance"/> for the failed invocation.
        /// </summary>
        public IFunctionInstance Instance { get; set; }
    }
}
