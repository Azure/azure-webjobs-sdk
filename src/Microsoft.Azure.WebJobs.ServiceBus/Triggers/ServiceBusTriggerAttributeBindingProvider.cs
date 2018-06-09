// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Converters;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.ServiceBus.Triggers
{
    internal class ServiceBusTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private static readonly IQueueTriggerArgumentBindingProvider InnerProvider =
            new CompositeArgumentBindingProvider(
                new ConverterArgumentBindingProvider<Message>(
                    new AsyncConverter<Message, Message>(new IdentityConverter<Message>())),
                new ConverterArgumentBindingProvider<string>(new MessageToStringConverter()),
                new ConverterArgumentBindingProvider<byte[]>(new MessageToByteArrayConverter()),
                new UserTypeArgumentBindingProvider()); // Must come last, because it will attempt to bind all types.

        private readonly INameResolver _nameResolver;
        private readonly ServiceBusOptions _config;
        private readonly MessagingProvider _messagingProvider;
        private readonly IConnectionStringProvider _connectionStringProvider;

        public ServiceBusTriggerAttributeBindingProvider(INameResolver nameResolver, ServiceBusOptions config, MessagingProvider messagingProvider, IConnectionStringProvider connectionStringProvider)
        {
            _nameResolver = nameResolver ?? throw new ArgumentNullException(nameof(nameResolver));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _messagingProvider = messagingProvider ?? throw new ArgumentNullException(nameof(messagingProvider));
            _connectionStringProvider = connectionStringProvider;
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

            string queueName = null;
            string topicName = null;
            string subscriptionName = null;
            string entityPath = null;

            if (attribute.QueueName != null)
            {
                queueName = Resolve(attribute.QueueName);
                entityPath = queueName;
            }
            else
            {
                topicName = Resolve(attribute.TopicName);
                subscriptionName = Resolve(attribute.SubscriptionName);
                entityPath = EntityNameHelper.FormatSubscriptionPath(topicName, subscriptionName);
            }

            ITriggerDataArgumentBinding<Message> argumentBinding = InnerProvider.TryCreate(parameter);
            if (argumentBinding == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "Can't bind ServiceBusTrigger to type '{0}'.", parameter.ParameterType));
            }

            ServiceBusAccount account = new ServiceBusAccount(_config, _connectionStringProvider, attribute);

            ITriggerBinding binding;
            if (queueName != null)
            {
                binding = new ServiceBusTriggerBinding(parameter.Name, parameter.ParameterType, argumentBinding, account, _config, _messagingProvider, queueName);
            }
            else
            {
                binding = new ServiceBusTriggerBinding(parameter.Name, parameter.ParameterType, argumentBinding, account, _config, _messagingProvider, topicName, subscriptionName);
            }

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
