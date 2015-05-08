﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Converters;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.ServiceBus.Bindings;
using Microsoft.Azure.WebJobs.ServiceBus.Listeners;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.Azure.WebJobs.ServiceBus.Triggers
{
    internal class ServiceBusTriggerBinding : ITriggerBinding<BrokeredMessage>
    {
        private readonly string _parameterName;
        private readonly IObjectToTypeConverter<BrokeredMessage> _converter;
        private readonly ITriggerDataArgumentBinding<BrokeredMessage> _argumentBinding;
        private readonly ServiceBusAccount _account;
        private readonly string _namespaceName;
        private readonly string _queueName;
        private readonly string _topicName;
        private readonly string _subscriptionName;
        private readonly string _entityPath;

        public ServiceBusTriggerBinding(string parameterName, Type parameterType, 
            ITriggerDataArgumentBinding<BrokeredMessage> argumentBinding, ServiceBusAccount account, string queueName)
        {
            _parameterName = parameterName;
            _converter = CreateConverter(parameterType);
            _argumentBinding = argumentBinding;
            _account = account;
            _namespaceName = ServiceBusClient.GetNamespaceName(account);
            _queueName = queueName;
            _entityPath = queueName;
        }

        public ServiceBusTriggerBinding(string parameterName, ITriggerDataArgumentBinding<BrokeredMessage> argumentBinding, 
            ServiceBusAccount account, string topicName, string subscriptionName)
        {
            _parameterName = parameterName;
            _argumentBinding = argumentBinding;
            _account = account;
            _namespaceName = ServiceBusClient.GetNamespaceName(account);
            _topicName = topicName;
            _subscriptionName = subscriptionName;
            _entityPath = SubscriptionClient.FormatSubscriptionPath(topicName, subscriptionName);
        }

        public Type TriggerValueType
        {
            get
            {
                return typeof(BrokeredMessage);
            }
        }

        public IReadOnlyDictionary<string, Type> BindingDataContract
        {
            get { return _argumentBinding.BindingDataContract; }
        }

        public string QueueName
        {
            get { return _queueName; }
        }

        public string TopicName
        {
            get { return _topicName; }
        }

        public string SubscriptionName
        {
            get { return _subscriptionName; }
        }

        public string EntityPath
        {
            get { return _entityPath; }
        }

        public async Task<ITriggerData> BindAsync(BrokeredMessage value, ValueBindingContext context)
        {
            ITriggerData triggerData = await _argumentBinding.BindAsync(value, context);
            return triggerData;
        }

        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            BrokeredMessage message = null;

            if (!_converter.TryConvert(value, out message))
            {
                throw new InvalidOperationException("Unable to convert trigger to BrokeredMessage.");
            }

            return BindAsync(message, context);
        }

        public IListenerFactory CreateListenerFactory(FunctionDescriptor descriptor, ITriggeredFunctionExecutor executor)
        {
            return CreateListenerFactory(descriptor, (ITriggeredFunctionExecutor<BrokeredMessage>)executor);
        }

        public IListenerFactory CreateListenerFactory(FunctionDescriptor descriptor, ITriggeredFunctionExecutor<BrokeredMessage> executor)
        {
            if (_queueName != null)
            {
                return new ServiceBusQueueListenerFactory(_account, _queueName, executor);
            }
            else
            {
                return new ServiceBusSubscriptionListenerFactory(_account, _topicName, _subscriptionName, executor);
            }
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            string entityPath = _queueName != null ?
                    _queueName : string.Format("{0}/Subscriptions/{1}", _topicName, _subscriptionName);

            return new ServiceBusTriggerParameterDescriptor
            {
                Name = _parameterName,
                NamespaceName = _namespaceName,
                QueueName = _queueName,
                TopicName = _topicName,
                SubscriptionName = _subscriptionName,
                DisplayHints = ServiceBusBinding.CreateParameterDisplayHints(entityPath, true)
            };
        }

        private static IObjectToTypeConverter<BrokeredMessage> CreateConverter(Type parameterType)
        {
            return new CompositeObjectToTypeConverter<BrokeredMessage>(
                    new OutputConverter<BrokeredMessage>(new IdentityConverter<BrokeredMessage>()),
                    new OutputConverter<string>(StringToBrokeredMessageConverterFactory.Create(parameterType)));

        }
    }
}
