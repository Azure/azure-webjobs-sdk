// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Azure.WebJobs.Host.Lease
{
    /// <summary>
    /// A Lease Exception
    /// </summary>
    [Serializable]
    public class LeaseException : Exception
    {
        /// <inheritdoc />
        public LeaseException() : base()
        {
        }

        /// <inheritdoc />
        public LeaseException(String message) : base(message)
        {
        }

        /// <inheritdoc />
        public LeaseException(String message, Exception innerException) : base(message, innerException)
        {
        }

        /// <inheritdoc />
        protected LeaseException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            FailureReason = (LeaseFailureReason)Enum.Parse(typeof(LeaseFailureReason), info.GetString("FailureReason"));
        }

        /// <inheritdoc />
        public LeaseException(LeaseFailureReason failureReason, Exception innerException)
            : this("Lease Exception", innerException)
        {
            FailureReason = failureReason;
        }

        /// <summary>
        /// Lease failure reason
        /// </summary>
        public LeaseFailureReason FailureReason { get; protected set; }

        /// <inheritdoc />
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            info.AddValue("FailureReason", FailureReason);

            base.GetObjectData(info, context);
        }
    }
}
