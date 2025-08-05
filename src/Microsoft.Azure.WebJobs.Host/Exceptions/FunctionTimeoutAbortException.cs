// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Exception thrown when a function invocation is aborted due to another concurrently running function timing out.
    /// </summary>
    public class FunctionTimeoutAbortException : FunctionTimeoutException
    {
        /// <inheritdoc/>
        public FunctionTimeoutAbortException() : base()
        {
        }

        /// <inheritdoc/>
        public FunctionTimeoutAbortException(string message) : base(message)
        {
        }

        /// <inheritdoc/>
        public FunctionTimeoutAbortException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
