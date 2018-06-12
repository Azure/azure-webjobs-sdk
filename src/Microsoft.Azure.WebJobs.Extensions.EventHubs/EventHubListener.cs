// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    // Created from the EventHubTrigger attribute to listen on the EventHub. 
    internal sealed class EventHubListener : IListener, IEventProcessorFactory
    {
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly EventProcessorHost _eventListener;
        private readonly bool _singleDispatch;
        private readonly EventProcessorOptions _options;
        private readonly EventHubConfiguration _config;
        private readonly ILogger _logger;
        private bool _started;

        public EventHubListener(ITriggeredFunctionExecutor executor, EventProcessorHost eventListener, bool single, EventHubConfiguration config, ILogger logger)
        {
            _executor = executor;
            _eventListener = eventListener;
            _singleDispatch = single;
            _options = config.GetOptions();
            _config = config;
            _logger = logger;
        }

        void IListener.Cancel()
        {
            StopAsync(CancellationToken.None).Wait();
        }

        void IDisposable.Dispose() // via IListener
        {
            // nothing to do. 
        }

        // This will get called once when starting the JobHost. 
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _eventListener.RegisterEventProcessorFactoryAsync(this, _options);
            _started = true;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_started)
            {
                await _eventListener.UnregisterEventProcessorAsync();
            }
            _started = false;
        }

        // This will get called per-partition. 
        IEventProcessor IEventProcessorFactory.CreateEventProcessor(PartitionContext context)
        {
            return new Listener(this, _logger);
        }

        internal static Func<Func<Task>, Task> CreateCheckpointStrategy(int batchCheckpointFrequency)
        {
            if (batchCheckpointFrequency <= 0)
            {
                throw new InvalidOperationException("Batch checkpoint frequency must be larger than 0.");
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

        // We get a new instance each time Start() is called. 
        // We'll get a listener per partition - so they can potentialy run in parallel even on a single machine.
        private class Listener : IEventProcessor
        {
            private readonly EventHubListener _parent;
            private readonly ILogger _logger;
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();
            private readonly Func<PartitionContext, Task> _checkpoint;


            public Listener(EventHubListener parent, ILogger logger)
            {
                _parent = parent;
                var checkpointStrategy = CreateCheckpointStrategy(parent._config.BatchCheckpointFrequency);
                _checkpoint = (context) => checkpointStrategy(context.CheckpointAsync);
                _logger = logger;
            }

            public async Task CloseAsync(PartitionContext context, CloseReason reason)
            {
                _cts.Cancel(); // Signal interuption to ProcessEventsAsync()

                // Finish listener
                if (reason == CloseReason.Shutdown)
                {
                    await context.CheckpointAsync();
                }
            }

            public Task OpenAsync(PartitionContext context)
            {
                return Task.FromResult(0);
            }

            public Task ProcessErrorAsync(PartitionContext context, Exception error)
            {
                string errorMessage = $"Error processing EventHub message from Partition Id:{context.PartitionId}, Owner:{context.Owner}, EventHubPath:{context.EventHubPath}";
                _logger.LogError(error, errorMessage);
                return Task.CompletedTask;
            }

            public async Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
            {
                EventHubTriggerInput value = new EventHubTriggerInput
                {
                    Events = messages.ToArray(),
                    PartitionContext = context
                };

                // Single dispatch 
                if (_parent._singleDispatch)
                {
                    int len = value.Events.Length;

                    List<Task> dispatches = new List<Task>();
                    for (int i = 0; i < len; i++)
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
                            Task task = _parent._executor.TryExecuteAsync(input, _cts.Token);
                            dispatches.Add(task);
                        }
                    }

                    // Drain the whole batch before taking more work
                    if (dispatches.Count > 0)
                    {
                        await Task.WhenAll(dispatches);
                    }
                }
                else
                {
                    // Batch dispatch
                    TriggeredFunctionData input = new TriggeredFunctionData
                    {
                        ParentId = null,
                        TriggerValue = value
                    };

                    FunctionResult result = await _parent._executor.TryExecuteAsync(input, _cts.Token);
                }

                bool hasEvents = false;

                // Dispose all messages to help with memory pressure. If this is missed, the finalizer thread will still get them. 
                foreach (var message in messages)
                {
                    hasEvents = true;
                    message.Dispose();
                }

                // Don't checkpoint if no events. This can reset the sequence counter to 0. 
                if (hasEvents)
                {
                    await _checkpoint(context);
                }
            }
        } // end class Listener 
    }
}