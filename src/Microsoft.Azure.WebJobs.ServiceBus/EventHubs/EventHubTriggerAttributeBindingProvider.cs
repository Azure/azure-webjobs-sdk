// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.ServiceBus.Messaging;
using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Listeners;
using System.Text;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.ServiceBus
{    
    internal class EventHubTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly INameResolver _nameResolver;
        private readonly IEventHubProvider _eventHubConfig;

        public EventHubTriggerAttributeBindingProvider(INameResolver nameResolver, IEventHubProvider eventHubConfig)
        {
            this._nameResolver = nameResolver;
            this._eventHubConfig = eventHubConfig;
        }

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

            string eventHubName = attribute.EventHubName;
            string resolvedName = _nameResolver.ResolveWholeString(eventHubName);
            var eventHostListener = _eventHubConfig.GetEventProcessorHost(resolvedName);
                        
            bool singleDispatch;

            var hooks = new EventHubTriggerBindingStrategy();
            var argumentBinding = GenericBinder.GetTriggerArgumentBinding(hooks, parameter, out singleDispatch);

            if (argumentBinding == null)
            {
                string msg = string.Format("Unsupported binder type: {0}. Bind to string[] or EventData[]", parameter.ParameterType);
                throw new InvalidOperationException(msg);
            }

            var options = _eventHubConfig.GetOptions();
            Func<ListenerFactoryContext, Task<IListener>> createListener =
                (factoryContext) =>
                {
                    IListener listener = new EventHubListener(factoryContext.Executor, eventHostListener, options, singleDispatch);
                    return Task.FromResult(listener);
                };

            var parameterDescriptor = new ParameterDescriptor
            {
                Name = parameter.Name,
                DisplayHints = new ParameterDisplayHints
                {
                      Description = "EventHub message"                       
                }
            };

            ITriggerBinding binding = new CommonTriggerBinding<EventData, EventHubTriggerInput>(hooks, argumentBinding, createListener, parameterDescriptor); 

            return Task.FromResult<ITriggerBinding>(binding);         
        }
    } // end class
}