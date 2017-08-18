// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    /// <summary>
    /// The EventHubOrderedEventListener class.
    /// </summary>
    internal class EventHubOrderedEventListener : IEventProcessor, IDisposable
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly ITriggeredFunctionExecutor _executor;

        private readonly bool _singleDispatch;
        private readonly EventHubDispatcher _dispatcher;
        private readonly TraceWriter _trace;
        private readonly Func<PartitionContext, Task> _checkpoint;
        private readonly int _orderedEventSlotCount;

        /// <summary>
        /// The EventHubOrderedEventListener.
        /// </summary>
        /// <param name="executor"></param>
        /// <param name="statusManager"></param>
        /// <param name="orderedListenerConfig"></param>
        /// <param name="trace"></param>
        public EventHubOrderedEventListener(
            ITriggeredFunctionExecutor executor,
            IMessageStatusManager statusManager,
            EventHubOrderedEventConfiguration orderedListenerConfig,
            TraceWriter trace)
        {
            this._singleDispatch = orderedListenerConfig.IsSingleDispatch;
            this._executor = executor;

            this._dispatcher = new EventHubDispatcher(
                executor: executor,
                statusManager: statusManager,
                maxElapsedTime: orderedListenerConfig.MaxElapsedTime,
                maxDop: orderedListenerConfig.MaxDegreeOfParallelism,
                capacity: orderedListenerConfig.BoundedCapacity);

            var checkpointStrategy = CreateCheckpointStrategy(orderedListenerConfig.BatchCheckpointFrequency);
            this._checkpoint = (context) => checkpointStrategy(context.CheckpointAsync);
            this._orderedEventSlotCount = orderedListenerConfig.MaxDegreeOfParallelism;
            _trace = trace;
            _trace.Info($"Event hub ordered listener: Max degree of parallelism:{orderedListenerConfig.MaxDegreeOfParallelism}, bounded capacity:{orderedListenerConfig.BoundedCapacity}");
        }

        public async Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            // Signal interuption to ProcessEventsAsync()
            this._cts.Cancel();

            // Finish listener
            if (reason == CloseReason.Shutdown)
            {
                await context.CheckpointAsync().ConfigureAwait(false);
            }
        }

        public Task OpenAsync(PartitionContext context)
        {
            return Task.FromResult(0);
        }

        public async Task ProcessEventsAsync(PartitionContext context,
            IEnumerable<EventData> messages)
        {
            // Event hub can return a null message set on timeout
            if (messages == null)
            {
                return;
            }

            EventData[] events = messages.ToArray();
            if (events.Length == 0)
            {
                return;
            }

            int messageCount = events.Length;

            // Single dispatch 
            if (_singleDispatch)
            {
                EventHubTriggerInput value = new EventHubTriggerInput
                {
                    Events = events,
                    PartitionContext = context
                };

                List<Task> dispatches = new List<Task>();
                for (int i = 0; i < messageCount; i++)
                {
                    if (_cts.IsCancellationRequested)
                    {
                        // If we stopped the listener, then we may lose the lease and be unable to checkpoint. 
                        // So skip running the rest of the batch. The new listener will pick it up. 
                        continue;
                    }
                    else
                    {
                        TriggeredFunctionData input = new TriggeredFunctionData
                        {
                            ParentId = null,
                            TriggerValue = value.GetSingleEventTriggerInput(i)
                        };

                        Task task = _executor.TryExecuteAsync(input, _cts.Token);
                        dispatches.Add(task);
                    }
                }

                // Drain the whole batch before taking more work
                if (dispatches.Count > 0)
                {
                    await Task.WhenAll(dispatches);
                }

                _trace.Info($"Event hub ordered listener: Single dispatch: Dispatched {dispatches.Count} messages.");
            }
            else
            {
                // Batch dispatch
                EventHubTriggerInput value = new EventHubTriggerInput
                {
                    Events = events,
                    PartitionContext = context,
                    OrderedEventSlotCount = this._orderedEventSlotCount
                };

                value.CreatePartitionKeyOrdering();

                List<Task> dispatches = new List<Task>();
                int dispatchedMessageCount = 0;
                for (int i = 0; i < value.OrderedEventSlotCount; i++)
                {
                    if (_cts.IsCancellationRequested)
                    {
                        // If we stopped the listener, then we may lose the lease and be unable to checkpoint. 
                        // So skip running the rest of the batch. The new listener will pick it up. 
                        continue;
                    }
                    else
                    {
                        // The entire batch of messages is passed to the dispatcher each 
                        // time, incrementing the selector index
                        var trigger = value.GetOrderedBatchEventTriggerInput(i);
                        if (trigger != null)
                        {
                            dispatchedMessageCount += trigger.Events.Length;

                            var task = await _dispatcher.SendAsync(new TriggeredFunctionData()
                            {
                                ParentId = null,
                                TriggerValue = trigger
                            });
                           dispatches.Add(task);
                        }
                    }
                }

                // Drain the whole batch before taking more work
                if (dispatches.Count > 0)
                {
                    await Task.WhenAll(dispatches).ConfigureAwait(false);
                }

                _trace.Info($"Event hub ordered listener: Batch dispatch: Dispatched {dispatchedMessageCount} messages.");
            }

            await _checkpoint(context).ConfigureAwait(false);

            // await context.CheckpointAsync().ConfigureAwait(false);
            _trace.Info($"Event hub ordered listener: Checkpointed {messageCount} messages.");

            foreach (var message in events)
            {
                message.Dispose();
            }

            foreach (var message in messages)
            {
                message.Dispose();
            }
        }

        public void Dispose()
        {
            _cts.Dispose();
            _dispatcher.Dispose();
        }

        internal static Func<Func<Task>, Task> CreateCheckpointStrategy(int batchCheckpointFrequency)
        {
            if (batchCheckpointFrequency <= 0)
            {
                throw new InvalidOperationException("Ordered listener checkpoint frequency must be larger than 0.");
            }
            else if (batchCheckpointFrequency == 1)
            {
                return (checkpoint) => checkpoint();
            }
            else
            {
                int batchCounter = 0;
                return async (checkpoint) =>
                {
                    batchCounter++;
                    if (batchCounter >= batchCheckpointFrequency)
                    {
                        batchCounter = 0;
                        await checkpoint();
                    }
                };
            }
        }
    }
}