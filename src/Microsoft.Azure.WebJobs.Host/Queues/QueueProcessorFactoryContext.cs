﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Microsoft.Azure.WebJobs.Host.Queues
{
    /// <summary>
    /// Provides context input for <see cref="IQueueProcessorFactory"/>.
    /// </summary>
    [CLSCompliant(false)]
    public class QueueProcessorFactoryContext
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="queue">The <see cref="CloudQueue"/> the <see cref="QueueProcessor"/> will operate on.</param>
        /// <param name="trace">The <see cref="TraceWriter"/> to trace events to.</param>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to create an <see cref="ILogger"/> from.</param>
        /// <param name="poisonQueue">The queue to move messages to when unable to process a message after the maximum dequeue count has been exceeded. May be null.</param>
        public QueueProcessorFactoryContext(CloudQueue queue, TraceWriter trace, ILoggerFactory loggerFactory, CloudQueue poisonQueue = null)
        {
            if (queue == null)
            {
                throw new ArgumentNullException("queue");
            }
            if (trace == null)
            {
                throw new ArgumentNullException("trace");
            }

            Queue = queue;
            PoisonQueue = poisonQueue;
            Trace = trace;
            Logger = loggerFactory?.CreateLogger(LogCategories.CreateTriggerCategory("Queue"));
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="queue">The <see cref="CloudQueue"/> the <see cref="QueueProcessor"/> will operate on.</param>
        /// <param name="trace">The <see cref="TraceWriter"/> to write to.</param>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to create an <see cref="ILogger"/> from.</param>
        /// <param name="queueConfiguration">The queue configuration.</param>
        /// <param name="poisonQueue">The queue to move messages to when unable to process a message after the maximum dequeue count has been exceeded. May be null.</param>
        internal QueueProcessorFactoryContext(CloudQueue queue, TraceWriter trace, ILoggerFactory loggerFactory, IQueueConfiguration queueConfiguration, CloudQueue poisonQueue = null)
            : this(queue, trace, loggerFactory, poisonQueue)
        {
            BatchSize = queueConfiguration.BatchSize;
            MaxDequeueCount = queueConfiguration.MaxDequeueCount;
            NewBatchThreshold = queueConfiguration.NewBatchThreshold;
            VisibilityTimeout = queueConfiguration.VisibilityTimeout;
            MaxPollingInterval = queueConfiguration.MaxPollingInterval;
            DeleteRetryCount = queueConfiguration.DeleteRetryCount;
        }

        /// <summary>
        /// Gets the <see cref="CloudQueue"/> the <see cref="QueueProcessor"/> will operate on.
        /// </summary>
        public CloudQueue Queue { get; private set; }

        /// <summary>
        /// Gets the <see cref="CloudQueue"/> for poison messages that the <see cref="QueueProcessor"/> will use.
        /// May be null.
        /// </summary>
        public CloudQueue PoisonQueue { get; private set; }

        /// <summary>
        /// Gets the <see cref="TraceWriter"/>.
        /// </summary>
        public TraceWriter Trace { get; private set; }

        /// <summary>
        /// Gets the <see cref="ILogger"/>. 
        /// </summary>
        public ILogger Logger { get; private set; }

        /// <summary>
        /// Gets or sets the number of queue messages to retrieve and process in parallel (per job method).
        /// </summary>
        public int BatchSize { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of times to try processing a message before moving
        /// it to the poison queue (if a poison queue is configured for the queue).
        /// </summary>
        public int MaxDequeueCount { get; set; }

        /// <summary>
        /// Gets or sets the threshold at which a new batch of messages will be fetched.
        /// </summary>
        public int NewBatchThreshold { get; set; }

        /// <summary>
        /// Gets or sets the longest period of time to wait before checking for a message to arrive when a queue remains
        /// empty.
        /// </summary>
        public TimeSpan MaxPollingInterval { get; set; }

        /// <summary>
        /// Gets or sets the message visibility that will be used for messages that
        /// fail processing.
        /// </summary>
        public TimeSpan VisibilityTimeout { get; set; }

        /// <summary>
        /// Gets or sets the number of retries for deleting the message
        /// </summary>
        public int DeleteRetryCount { get; set; }
    }
}
