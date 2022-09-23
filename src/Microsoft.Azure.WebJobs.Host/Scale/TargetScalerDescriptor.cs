// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host.Scale
{

    /// <summary>
    /// Metadata descriptor for an <see cref="ITargetScaler"/>.
    /// </summary>
    public class TargetScalerDescriptor
    {
        public TargetScalerDescriptor(string id, string functionId)
        {
            Id = id;
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
        /// Gets the unique function id.
        /// </summary>
        /// <remarks>
        /// This should be constant. It is used to get persisted dynamic concurrency value.
        /// </remarks>
        public string FunctionId { get; }
    }
}
