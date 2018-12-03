// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;

namespace Microsoft.Azure.WebJobs.ServiceBus.Listeners
{
    internal class ServiceBusSubscriptionListenerFactory : IListenerFactory
    {
        private readonly MessagingProvider _messagingProvider;
        private readonly string _topicName;
        private readonly string _subscriptionName;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly ServiceBusOptions _options;

        public ServiceBusSubscriptionListenerFactory(MessagingProvider messagingProvider, string topicName, string subscriptionName, ITriggeredFunctionExecutor executor, ServiceBusOptions options)
        {
            _messagingProvider = messagingProvider;
            _topicName = topicName;
            _subscriptionName = subscriptionName;
            _executor = executor;
            _options = options;
        }

        public Task<IListener> CreateAsync(CancellationToken cancellationToken)
        {
            string entityPath = EntityNameHelper.FormatSubscriptionPath(_topicName, _subscriptionName);

            ServiceBusTriggerExecutor triggerExecutor = new ServiceBusTriggerExecutor(_executor);
            var listener = new ServiceBusListener(entityPath, triggerExecutor, _messagingProvider);

            return Task.FromResult<IListener>(listener);
        }
    }
}
