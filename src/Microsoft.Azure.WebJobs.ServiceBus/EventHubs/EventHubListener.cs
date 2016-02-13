// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.ServiceBus.Messaging;
using Microsoft.Azure.WebJobs.Host.Protocols;
using System.Globalization;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    // Created from the EventHubTrigger attribute to listen on the EventHub. 
    internal sealed class EventHubListener : IListener, IEventProcessor, IEventProcessorFactory
    {
        private ITriggeredFunctionExecutor _executor;
        private readonly EventProcessorHost _eventListener;
        private readonly bool _singleDispatch;
        private readonly EventProcessorOptions _options;

        public EventHubListener(ITriggeredFunctionExecutor executor, EventProcessorHost eventListener, EventProcessorOptions options, bool single)
        {
            this._executor = executor;
            this._eventListener = eventListener;
            this._singleDispatch = single;
            this._options = options;
        }

        void IListener.Cancel()
        {
            // $$$ Difference between Cancel and Stop?
            this.StopAsync(CancellationToken.None).Wait();
        }

        void IDisposable.Dispose() // via IListener
        {
            // nothing to do. 
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {            
            return _eventListener.RegisterEventProcessorFactoryAsync(this, _options);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _eventListener.UnregisterEventProcessorAsync();
        }


        async Task IEventProcessor.OpenAsync(PartitionContext context)
        {
            // Begin listener 
        }

        async Task IEventProcessor.ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
        {
            EventHubTriggerInput value = new EventHubTriggerInput
            {
                _events = messages.ToArray(),
                _context = context
            };

            // Single dispatch 
            if (_singleDispatch)
            {
                int len = value._events.Length;

                Task[] dispatches = new Task[len];
                for (int i = 0; i < len; i++)
                {
                    TriggeredFunctionData input = new TriggeredFunctionData
                    {
                        ParentId = null,
                        TriggerValue = value.GetSingleEvent(i)
                    };
                    dispatches[i] = _executor.TryExecuteAsync(input, CancellationToken.None);
                }

                // Drain the whole batch before taking more work
                await Task.WhenAll(dispatches);
            }
            else
            {
                // Batch dispatch

                TriggeredFunctionData input = new TriggeredFunctionData
                {
                    ParentId = null,
                    TriggerValue = value
                };

                FunctionResult result = await _executor.TryExecuteAsync(input, CancellationToken.None);
            }

            // $$$ Only if succeeded? What about retry? 
            await context.CheckpointAsync();        
        }

        async Task IEventProcessor.CloseAsync(PartitionContext context, CloseReason reason)
        {
            // Finish listener
            if (reason == CloseReason.Shutdown)
            {
                await context.CheckpointAsync();
            }
        }

        IEventProcessor IEventProcessorFactory.CreateEventProcessor(PartitionContext context)
        {
            return this;
        }
    }

}