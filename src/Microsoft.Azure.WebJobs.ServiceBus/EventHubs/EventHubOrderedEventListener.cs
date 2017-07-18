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

        /// <summary>
        /// The EventHubOrderedEventListener.
        /// </summary>
        /// <param name="executor"></param>
        /// <param name="statusManager"></param>
        /// <param name="streamListenerConfig"></param>
        /// <param name="trace"></param>
        public EventHubOrderedEventListener(
            ITriggeredFunctionExecutor executor,
            IMessageStatusManager statusManager,
            EventHubOrderedEventConfiguration streamListenerConfig,
            TraceWriter trace)
        {
            this._singleDispatch = streamListenerConfig.IsSingleDispatch;
            this._executor = executor;

            this._dispatcher = new EventHubDispatcher(
                executor: executor,
                statusManager: statusManager,
                maxElapsedTime: streamListenerConfig.MaxElapsedTime,
                maxDop: streamListenerConfig.MaxDegreeOfParallelism,
                capacity: streamListenerConfig.BoundedCapacity);

            var checkpointStrategy = CreateCheckpointStrategy(streamListenerConfig.BatchCheckpointFrequency);
            this._checkpoint = (context) => checkpointStrategy(context.CheckpointAsync);
            _trace = trace;
            _trace.Info($"Event hub stream listener: Max degree of parallelism:{streamListenerConfig.MaxDegreeOfParallelism}, bounded capacity:{streamListenerConfig.BoundedCapacity}");
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

            EventHubTriggerInput value = new EventHubTriggerInput
            {
                Events = messages.ToArray(),
                PartitionContext = context
            };

            int messageCount = value.Events.Length;

            // Single dispatch 
            if (_singleDispatch)
            {
                List<Task> dispatches = new List<Task>();
                for (int i = 0; i < events.Length; i++)
                {
                    // The entire batch of messages is passed to the dispatcher each 
                    // time, incrementing the selector index
                    var trigger = value.GetSingleEventTriggerInput(i);

                    var task = _dispatcher.SendAsync(new TriggeredFunctionData()
                    {
                        ParentId = null,
                        TriggerValue = trigger
                    });
                    dispatches.Add(task);
                }

                int dispatchCount = dispatches.Count;
                // Drain the whole batch before taking more work
                if (dispatches.Count > 0)
                {
                    await Task.WhenAll(dispatches).ConfigureAwait(false);
                }

                _trace.Info($"Event hub stream listener: Single dispatch: Dispatched {dispatchCount} messages.");
            }
            else
            {
                // Batch dispatch
                TriggeredFunctionData input = new TriggeredFunctionData
                {
                    ParentId = null,
                    TriggerValue = value
                };

                // TODO: Replace _executor with _dispatcher
                FunctionResult result = await _executor
                    .TryExecuteAsync(input, CancellationToken.None)
                    .ConfigureAwait(false);

                // Dispose all messages to help with memory pressure. If this is missed, the finalizer thread will still get them. 
            }

            await _checkpoint(context).ConfigureAwait(false);

            // await context.CheckpointAsync().ConfigureAwait(false);
            _trace.Info($"Event hub stream listener: Checkpointed {messageCount} messages.");

            foreach (var message in events)
            {
                message.Dispose();
            }

            foreach (var message in messages)
            {
                message.Dispose();
            }
        }

        private static byte[][] GetContent(EventData[] messages)
        {
            var bytes = new List<byte[]>();
            for (int i = 0; i < messages.Length; i++)
            {
                var content = messages[i].GetBytes();
                bytes.Add(content);
            }
            return bytes.ToArray();
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
                throw new InvalidOperationException("Stream listener checkpoint frequency must be larger than 0.");
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