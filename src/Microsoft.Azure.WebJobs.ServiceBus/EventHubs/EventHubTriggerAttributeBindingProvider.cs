// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    internal class EventHubTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly INameResolver _nameResolver;
        private readonly EventHubConfiguration _eventHubConfig;
        private readonly MessagingExceptionHandler _exceptionHandler;
        private readonly IConverterManager _converterManager;

        public EventHubTriggerAttributeBindingProvider(
            INameResolver nameResolver,
            IConverterManager converterManager,
            EventHubConfiguration eventHubConfig,
            MessagingExceptionHandler exceptionHandler)
        {
            _nameResolver = nameResolver;
            _converterManager = converterManager;
            _eventHubConfig = eventHubConfig;
            _exceptionHandler = exceptionHandler;
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
            string resolvedConsumerGroup = _nameResolver.ResolveWholeString(attribute.ConsumerGroup ?? EventHubConsumerGroup.DefaultGroupName);

            EventProcessorHost eventProcessorHost;

            if (!string.IsNullOrWhiteSpace(attribute.Connection))
            {
                string resolvedConnectionString = _nameResolver.Resolve(attribute.Connection);
                _eventHubConfig.AddReceiver(resolvedEventHubName, resolvedConnectionString);

                eventProcessorHost = _eventHubConfig.GetEventProcessorHost(resolvedEventHubName, resolvedConsumerGroup, resolvedConnectionString);
            }
            else
            {
                eventProcessorHost = _eventHubConfig.GetEventProcessorHost(resolvedEventHubName, resolvedConsumerGroup);
            }

            Func<ListenerFactoryContext, bool, Task<IListener>> createListener =
             (factoryContext, singleDispatch) =>
             {
                 IListener listener = new EventHubListener(factoryContext.Executor, eventProcessorHost, singleDispatch, _eventHubConfig, _exceptionHandler);
                 return Task.FromResult(listener);
             };

            ITriggerBinding binding = BindingFactory.GetTriggerBinding(new EventHubTriggerBindingStrategy(), parameter, _converterManager, createListener);
            return Task.FromResult<ITriggerBinding>(binding);
        }
    } // end class
}