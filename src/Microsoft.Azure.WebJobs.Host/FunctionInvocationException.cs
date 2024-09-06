﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Logging;
using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Exception thrown when a job function invocation fails.
    /// </summary>
    [Serializable]
    public class FunctionInvocationException : FunctionException
    {
        /// <inheritdoc/>
        public FunctionInvocationException() : base()
        {
        }

        /// <inheritdoc/>
        public FunctionInvocationException(string message) : base(Sanitizer.Sanitize(message))
        {
        }

        /// <inheritdoc/>
        public FunctionInvocationException(string message, Exception innerException)
            : base(Sanitizer.Sanitize(message), UpdateMessage(innerException, Sanitizer.Sanitize(innerException?.Message)))
        {
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/>.</param>
        /// <param name="context">The <see cref="StreamingContext"/>.</param>
        protected FunctionInvocationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            InstanceId = Guid.Parse(info.GetString("InstanceId"));
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="instanceId">The function instance Id.</param>
        /// <param name="methodName">The fully qualified method name.</param>
        /// <param name="innerException">The exception that is the cause of the current exception (or null).</param>
        public FunctionInvocationException(string message, Guid instanceId, string methodName, Exception innerException)
            : base(Sanitizer.Sanitize(message), UpdateMessage(innerException, Sanitizer.Sanitize(innerException?.Message)))
        {
            InstanceId = instanceId;
        }

        /// <summary>
        /// Gets the instance Id of the failed invocation. This value can be correlated
        /// to the Dashboard logs.
        /// </summary>
        public Guid InstanceId { get; set; }

        /// <inheritdoc/>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            info.AddValue("InstanceId", this.InstanceId);

            base.GetObjectData(info, context);
        }

        private static Exception UpdateMessage(Exception exception, string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return exception;
            }

            // Use reflection to set the private '_message' field of the Exception object
            FieldInfo messageField = typeof(Exception).GetField("_message", BindingFlags.Instance | BindingFlags.NonPublic);
            if (messageField != null)
            {
                messageField.SetValue(exception, message);
            }

            return exception;
        }
    }
}
