﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.Azure.WebJobs.ServiceBus.Listeners
{
    internal class ServiceBusSubscriptionListenerFactory : IListenerFactory
    {
        private readonly NamespaceManager _namespaceManager;
        private readonly MessagingFactory _messagingFactory;
        private readonly OnMessageOptions _onMessageOptions;
        private readonly string _topicName;
        private readonly string _subscriptionName;
        private readonly ITriggeredFunctionExecutor<BrokeredMessage> _executor;
        private readonly AccessRights _accessRights;

        public ServiceBusSubscriptionListenerFactory(ServiceBusAccount account, OnMessageOptions onMessageOptions, string topicName, string subscriptionName, ITriggeredFunctionExecutor<BrokeredMessage> executor, AccessRights accessRights)
        {
            _namespaceManager = account.NamespaceManager;
            _messagingFactory = account.MessagingFactory;
            _onMessageOptions = onMessageOptions;
            _topicName = topicName;
            _subscriptionName = subscriptionName;
            _executor = executor;
            _accessRights = accessRights;
        }

        public async Task<IListener> CreateAsync(ListenerFactoryContext context)
        {
            if (_accessRights == AccessRights.Manage)
            {
                // Must create all messaging entities before creating message receivers and calling OnMessage.
                // Otherwise, some function could start to execute and try to output messages to entities that don't yet
                // exist.
                await _namespaceManager.CreateTopicIfNotExistsAsync(_topicName, context.CancellationToken);
                await _namespaceManager.CreateSubscriptionIfNotExistsAsync(_topicName, _subscriptionName, context.CancellationToken);
            }

            string entityPath = SubscriptionClient.FormatSubscriptionPath(_topicName, _subscriptionName);

            ServiceBusTriggerExecutor triggerExecutor = new ServiceBusTriggerExecutor(_executor);
            return new ServiceBusListener(_messagingFactory, entityPath, triggerExecutor, _onMessageOptions);
        }
    }
}
