// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.EventHubs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Logging;

namespace Microsoft.Azure.WebJobs.EventHubs
{
    internal class EventHubTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly INameResolver _nameResolver;
        private readonly ILogger _logger;
        private readonly EventHubConfiguration _eventHubConfig;
        private readonly IConverterManager _converterManager;

        public EventHubTriggerAttributeBindingProvider(
            INameResolver nameResolver,
            IConverterManager converterManager,
            EventHubConfiguration eventHubConfig,
            ILoggerFactory loggerFactory)
        {
            _nameResolver = nameResolver;
            _converterManager = converterManager;
            _eventHubConfig = eventHubConfig;
            _logger = loggerFactory?.CreateLogger(LogCategories.CreateTriggerCategory("EventHub"));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        public Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            EventHubTriggerAttribute attribute = parameter.GetCustomAttribute<EventHubTriggerAttribute>(inherit: false);

            if (attribute == null)
            {
                return Task.FromResult<ITriggerBinding>(null);
            }

            string resolvedEventHubName = _nameResolver.ResolveWholeString(attribute.EventHubName);

            string consumerGroup = attribute.ConsumerGroup ?? PartitionReceiver.DefaultConsumerGroupName;
            string resolvedConsumerGroup = _nameResolver.ResolveWholeString(consumerGroup);

            if (!string.IsNullOrWhiteSpace(attribute.Connection))
            {
                _eventHubConfig.AddReceiver(resolvedEventHubName, _nameResolver.Resolve(attribute.Connection));
            }
            
            var eventHostListener = _eventHubConfig.GetEventProcessorHost(resolvedEventHubName, resolvedConsumerGroup);

            Func<ListenerFactoryContext, bool, Task<IListener>> createListener =
             (factoryContext, singleDispatch) =>
             {
                 IListener listener = new EventHubListener(factoryContext.Executor, eventHostListener, singleDispatch, _eventHubConfig, _logger);
                 return Task.FromResult(listener);
             };

            ITriggerBinding binding = BindingFactory.GetTriggerBinding(new EventHubTriggerBindingStrategy(), parameter, _converterManager, createListener);
            return Task.FromResult<ITriggerBinding>(binding);         
        }
    } // end class
}