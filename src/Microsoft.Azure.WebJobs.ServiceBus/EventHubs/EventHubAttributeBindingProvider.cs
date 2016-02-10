// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.ServiceBus.Messaging;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.ServiceBus
{

    internal class EventHubAttributeBindingProvider : IBindingProvider
    {
        private INameResolver _nameResolver;
        private IEventHubProvider _eventHubConfig;

        public EventHubAttributeBindingProvider(INameResolver nameResolver, IEventHubProvider _eventHubConfig)
        {
            this._nameResolver = nameResolver;
            this._eventHubConfig = _eventHubConfig;
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            EventHubAttribute attribute = parameter.GetCustomAttribute<EventHubAttribute>(inherit: false);

            if (attribute == null)
            {
                return Task.FromResult<IBinding>(null);
            }

            string name = attribute.EventHubName;
            var resolvedName = _nameResolver.ResolveWholeString(name);
            var eventHubClient = _eventHubConfig.GetSender(resolvedName);

            IBinding binding = GenericBinder.BindCollector<EventData, EventHubClient>(parameter, eventHubClient,
              (client, valueBindingContext) => new EventHubAsyncCollector(client)
              );

            return Task.FromResult(binding);
        }
    }
}