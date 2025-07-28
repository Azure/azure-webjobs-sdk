// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Exception thrown when a job function invocation is aborted.
    /// </summary>
    public class FunctionAbortedException : FunctionTimeoutException
    {
        /// <inheritdoc/>
        public FunctionAbortedException() : base()
        {
        }

        /// <inheritdoc/>
        public FunctionAbortedException(string message) : base(message)
        {
        }

        /// <inheritdoc/>
        public FunctionAbortedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
