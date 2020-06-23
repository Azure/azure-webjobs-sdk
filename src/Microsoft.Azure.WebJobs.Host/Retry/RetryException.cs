// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// The exception that is thrown by listener when a retry is needed. The retry setting is specified using RetryResult.
    /// </summary>
    public class RetryException : Exception
    {
        public RetryException(RetryResult retryResult)
        {
            RetryResult = retryResult;
        }

        /// <summary>
        /// Gets or sets RestryResult for the RetryException.
        /// </summary>
        public RetryResult RetryResult { get; set; }
    }
}
