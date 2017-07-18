// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    /// <summary>
    /// Provide configuration for EventHubOrderedMessageConfiguration. 
    /// </summary>
    public class EventHubOrderedEventConfiguration
    {
        /// <summary>
        /// The EventHubOrderedMessageConfiguration
        /// </summary>
        /// <param name="isSingleDispatch"></param>
        /// <param name="maxElapsedTime"></param>
        /// <param name="maxDop"></param>
        /// <param name="boundedCapacity"></param>
        /// <param name="batchCheckpointFrequency"></param>
        public EventHubOrderedEventConfiguration(bool isSingleDispatch, TimeSpan maxElapsedTime, int maxDop, int boundedCapacity, int batchCheckpointFrequency)
        {
            this.IsSingleDispatch = isSingleDispatch;
            this.MaxElapsedTime = maxElapsedTime;
            this.MaxDegreeOfParallelism = maxDop;
            this.BoundedCapacity = boundedCapacity;
            this.BatchCheckpointFrequency = batchCheckpointFrequency;
        }

        /// <summary>
        /// Single dispatcher
        /// </summary>
        public bool IsSingleDispatch { get; private set; }

        /// <summary>
        /// MaxElapsedTime
        /// </summary>
        public TimeSpan MaxElapsedTime { get; private set; }

        /// <summary>
        /// The max degree of parallelism for TPL action block
        /// </summary>
        public int MaxDegreeOfParallelism { get; private set; }

        /// <summary>
        /// The bounded capacity for TPL action block
        /// </summary>
        public int BoundedCapacity { get; private set; }

        /// <summary>
        /// The batch checkpoint frequency, (e.g., 1 => checkpoint every batch, 3 => checkpoint after 3rd batch)
        /// </summary>
        public int BatchCheckpointFrequency { get; private set; }
    }
}