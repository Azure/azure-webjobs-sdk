// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    // Created from the EventHubTrigger attribute to listen on the EventHub. 
    internal sealed class EventHubListener : IListener, IEventProcessorFactory
    {
        private const int MaxElapsedTimeInMinutes = 5;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly EventProcessorHost _eventListener;
        private readonly bool _singleDispatch;
        private readonly EventProcessorOptions _options;
        private readonly IMessageStatusManager _statusManager;
        private readonly EventHubConfiguration _config;
        private readonly TraceWriter _trace;
        
        public EventHubListener(ITriggeredFunctionExecutor executor, IMessageStatusManager statusManager, EventProcessorHost eventListener, bool single, EventHubConfiguration config)
        {
            this._executor = executor;
            this._eventListener = eventListener;
            this._singleDispatch = single;
            this._config = config;
            this._options = config.GetOptions();
            this._statusManager = statusManager;
            this._trace = config.GetTraceWriter();
        }

        void IListener.Cancel()
        {
            this.StopAsync(CancellationToken.None).Wait();
        }

        void IDisposable.Dispose() // via IListener
        {
            // nothing to do. 
        }

        // This will get called once when starting the JobHost. 
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return _eventListener.RegisterEventProcessorFactoryAsync(this, _options);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _eventListener.UnregisterEventProcessorAsync();
        }

        // This will get called per-partition. 
        IEventProcessor IEventProcessorFactory.CreateEventProcessor(PartitionContext context)
        {
            if (this._config.PartitionKeyOrdering == true)
            {
                int orderedDispatcherMaxDop = this._options.MaxBatchSize / 16;
                int maxDop = (orderedDispatcherMaxDop >= 2) ? orderedDispatcherMaxDop : 2;
                int boundedCapacity = this._options.MaxBatchSize;
                int maxElapsedTimeInSeconds = 10; // (MaxElapsedTimeInMinutes * 60) / maxDop;

                EventHubOrderedEventConfiguration orderEventListenerConfig = new EventHubOrderedEventConfiguration(
                    _singleDispatch,
                    TimeSpan.FromSeconds(maxElapsedTimeInSeconds),
                    maxDop,
                    boundedCapacity,
                    this._config.BatchCheckpointFrequency);

                return new EventHubOrderedEventListener(this._executor, _statusManager, orderEventListenerConfig, _trace);
            }

            return new EventHubUnorderedEventListener(this._singleDispatch, this._executor, this._config.BatchCheckpointFrequency, _trace);
        }
    }
}