// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Metadata descriptor for an <see cref="IScaleMonitor"/>.
    /// </summary>
    public class ScaleMonitorDescriptor
    {
        [Obsolete("This constructor is obsolete. Use the version that takes function id instead.")]
        public ScaleMonitorDescriptor(string id)
        {
            Id = id;
        }

        public ScaleMonitorDescriptor(string id, string functionId) : this(id)
        {
            FunctionId = functionId;
        }

        /// <summary>
        /// Gets the unique ID for the monitor.
        /// </summary>
        /// <remarks>
        /// This should be constant. It is used to correlate persisted metrics samples
        /// with their corresponding monitor instance. E.g. for a QueueTrigger, this might
        /// be of the form "{FunctionId}-QueueTrigger-{QueueName}".
        /// </remarks>
        public string Id { get; }

        /// <summary>
        /// Gets the ID of the function associated with this monitor.
        /// </summary>
        public string FunctionId { get; }
    }
}
