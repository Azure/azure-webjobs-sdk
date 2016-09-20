﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Exception thrown when a job function invocation fails due to a timeout.
    /// </summary>
    [Serializable]
    public class FunctionTimeoutException : FunctionInvocationException
    {
        /// <inheritdoc/>
        public FunctionTimeoutException() : base()
        {
        }

        /// <inheritdoc/>
        public FunctionTimeoutException(string message) : base(message)
        {
        }

        /// <inheritdoc/>
        public FunctionTimeoutException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <inheritdoc/>
        protected FunctionTimeoutException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        /// <inheritdoc/>
        public FunctionTimeoutException(string message, Guid instanceId, string methodName, TimeSpan timeout, Task functionTask, Exception innerException)
            : base(message, instanceId, methodName, innerException)
        {
            Timeout = timeout;
            FunctionTask = functionTask;
        }

        /// <summary>
        /// The function timeout value that expired.
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// The task that did not complete due to a timeout.
        /// </summary>
        public Task FunctionTask { get; set; }

        /// <inheritdoc/>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            info.AddValue("Timeout", this.Timeout);

            base.GetObjectData(info, context);
        }
    }
}
