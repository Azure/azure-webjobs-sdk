// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.ServiceBus.Listeners;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.ServiceBus.Triggers
{
    internal class ServiceBusTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly INameResolver _nameResolver;
        private readonly ServiceBusOptions _options;
        private readonly MessagingProvider _messagingProvider;
        private readonly IConfiguration _configuration;
        private readonly IConverterManager _converterManager;

        public ServiceBusTriggerAttributeBindingProvider(INameResolver nameResolver, ServiceBusOptions options, MessagingProvider messagingProvider, IConfiguration configuration, IConverterManager converterManager)
        {
            _nameResolver = nameResolver ?? throw new ArgumentNullException(nameof(nameResolver));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _messagingProvider = messagingProvider ?? throw new ArgumentNullException(nameof(messagingProvider));
            _configuration = configuration;
            _converterManager = converterManager;
        }

        public Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            var attribute = TypeUtility.GetResolvedAttribute<ServiceBusTriggerAttribute>(parameter);

            if (attribute == null)
            {
                return Task.FromResult<ITriggerBinding>(null);
            }

            string entityPath = null;
            if (!string.IsNullOrEmpty(attribute.QueueName))
            {
                entityPath = Resolve(attribute.QueueName);
            }
            else if (!string.IsNullOrEmpty(attribute.TopicName) && !string.IsNullOrEmpty(attribute.SubscriptionName))
            {
                entityPath = EntityNameHelper.FormatSubscriptionPath(attribute.TopicName, attribute.SubscriptionName);
            }

            attribute.Connection = Resolve(attribute.Connection);
            ServiceBusAccount account = new ServiceBusAccount(_options, _configuration, entityPath, attribute, attribute.IsSessionsEnabled);

            Func<ListenerFactoryContext, bool, Task<IListener>> createListener =
            (factoryContext, singleDispatch) =>
            {
                IListener listener = new ServiceBusListener(factoryContext.Executor, _options, account, _messagingProvider, singleDispatch);
                return Task.FromResult(listener);
            };

            ParameterDisplayHints hints = new ParameterDisplayHints
            {
                Description = string.Format(CultureInfo.CurrentCulture, "dequeue from '{0}'", entityPath),
                Prompt = "Enter the queue message body",
                DefaultValue = null
            };

            ServiceBusTriggerParameterDescriptor parameterDescriptor = new ServiceBusTriggerParameterDescriptor
            {
                Name = parameter.Name,
                EntityPath = entityPath,
                DisplayHints = hints
            };

            ITriggerBinding binding = BindingFactory.GetTriggerBinding(new ServiceBusTriggerBindingStrategy(), parameter, _converterManager, createListener, parameterDescriptor);

            return Task.FromResult<ITriggerBinding>(binding);
        }

        private string Resolve(string queueName)
        {
            if (_nameResolver == null)
            {
                return queueName;
            }

            return _nameResolver.ResolveWholeString(queueName);
        }
    }
}
