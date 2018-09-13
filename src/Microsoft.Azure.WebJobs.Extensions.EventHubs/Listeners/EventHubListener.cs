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

namespace Microsoft.Azure.WebJobs.EventHubs
{
    internal sealed class EventHubListener : IListener, IEventProcessorFactory
    {
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly EventProcessorHost _eventProcessorHost;
        private readonly bool _singleDispatch;
        private readonly EventHubOptions _options;
        private readonly ILogger _logger;
        private bool _started;

        public EventHubListener(ITriggeredFunctionExecutor executor, EventProcessorHost eventProcessorHost, bool singleDispatch, EventHubOptions options, ILogger logger)
        {
            _executor = executor;
            _eventProcessorHost = eventProcessorHost;
            _singleDispatch = singleDispatch;
            _options = options;
            _logger = logger;
        }

        void IListener.Cancel()
        {
            StopAsync(CancellationToken.None).Wait();
        }

        void IDisposable.Dispose()
        {
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _eventProcessorHost.RegisterEventProcessorFactoryAsync(this, _options.EventProcessorOptions);
            _started = true;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_started)
            {
                await _eventProcessorHost.UnregisterEventProcessorAsync();
            }
            _started = false;
        }

        IEventProcessor IEventProcessorFactory.CreateEventProcessor(PartitionContext context)
        {
            return new EventProcessor(_options, _executor, _logger, _singleDispatch);
        }

        /// <summary>
        /// Wrapper for un-mockable checkpoint APIs to aid in unit testing
        /// </summary>
        internal interface ICheckpointer
        {
            Task CheckpointAsync(PartitionContext context);
        }

        // We get a new instance each time Start() is called. 
        // We'll get a listener per partition - so they can potentialy run in parallel even on a single machine.
        internal class EventProcessor : IEventProcessor, IDisposable, ICheckpointer
        {
            private readonly ITriggeredFunctionExecutor _executor;
            private readonly bool _singleDispatch;
            private readonly ILogger _logger;
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();
            private readonly ICheckpointer _checkpointer;
            private readonly int _batchCheckpointFrequency;
            private int _batchCounter = 0;
            private bool _disposed = false;

            public EventProcessor(EventHubOptions options, ITriggeredFunctionExecutor executor, ILogger logger, bool singleDispatch, ICheckpointer checkpointer = null)
            {
                _checkpointer = checkpointer ?? this;
                _executor = executor;
                _singleDispatch = singleDispatch;
                _batchCheckpointFrequency = options.BatchCheckpointFrequency;
                _logger = logger;
            }

            public Task CloseAsync(PartitionContext context, CloseReason reason)
            {
                // signal cancellation for any in progress executions 
                _cts.Cancel();

                return Task.CompletedTask;
            }

            public Task OpenAsync(PartitionContext context)
            {
                return Task.CompletedTask;
            }

            public Task ProcessErrorAsync(PartitionContext context, Exception error)
            {
                string errorMessage = $"Error processing event from Partition Id:{context.PartitionId}, Owner:{context.Owner}, EventHubPath:{context.EventHubPath}";
                _logger.LogError(error, errorMessage);

                return Task.CompletedTask;
            }

            public async Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
            {
                var triggerInput = new EventHubTriggerInput
                {
                    Events = messages.ToArray(),
                    PartitionContext = context
                };

                if (_singleDispatch)
                {
                    // Single dispatch
                    int eventCount = triggerInput.Events.Length;
                    List<Task> invocationTasks = new List<Task>();
                    for (int i = 0; i < eventCount; i++)
                    {
                        if (_cts.IsCancellationRequested)
                        {
                            break;
                        }

                        var input = new TriggeredFunctionData
                        {
                            TriggerValue = triggerInput.GetSingleEventTriggerInput(i)
                        };
                        Task task = _executor.TryExecuteAsync(input, _cts.Token);
                        invocationTasks.Add(task);
                    }

                    // Drain the whole batch before taking more work
                    if (invocationTasks.Count > 0)
                    {
                        await Task.WhenAll(invocationTasks);
                    }
                }
                else
                {
                    // Batch dispatch
                    var input = new TriggeredFunctionData
                    {
                        TriggerValue = triggerInput
                    };

                    await _executor.TryExecuteAsync(input, _cts.Token);
                }
                // Dispose all messages to help with memory pressure. If this is missed, the finalizer thread will still get them.
                bool hasEvents = false;
                foreach (var message in messages)
                {
                    hasEvents = true;
                    message.Dispose();
                }

                // Checkpoint if we procesed any events.
                // Don't checkpoint if no events. This can reset the sequence counter to 0.
                // Note: we intentionally checkpoint the batch regardless of function 
                // success/failure. EventHub doesn't support any sort "poison event" model,
                // so that is the responsibility of the user's function currently. E.g.
                // the function should have try/catch handling around all event processing
                // code, and capture/log/persist failed events, since they won't be retried.
                if (hasEvents)
                {
                    await CheckpointAsync(context);
                }
            }

            private async Task CheckpointAsync(PartitionContext context)
            {
                if (_batchCheckpointFrequency == 1)
                {
                    await _checkpointer.CheckpointAsync(context);
                }
                else
                {
                    // only checkpoint every N batches
                    if (++_batchCounter >= _batchCheckpointFrequency)
                    {
                        _batchCounter = 0;
                        await _checkpointer.CheckpointAsync(context);
                    }
                }
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!_disposed)
                {
                    if (disposing)
                    {
                        _cts.Dispose();
                    }

                    _disposed = true;
                }
            }

            public void Dispose()
            {
                Dispose(true);
            }

            async Task ICheckpointer.CheckpointAsync(PartitionContext context)
            {
                await context.CheckpointAsync();
            }
        } 
    }
}