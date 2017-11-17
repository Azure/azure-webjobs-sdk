// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.ServiceBus;

namespace Microsoft.Azure.WebJobs.ServiceBus.Listeners
{
    internal class ServiceBusSubscriptionListenerFactory : IListenerFactory
    {
        private readonly ServiceBusAccount _account;
        private readonly string _topicName;
        private readonly string _subscriptionName;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly ServiceBusConfiguration _config;

        public ServiceBusSubscriptionListenerFactory(ServiceBusAccount account, string topicName, string subscriptionName, ITriggeredFunctionExecutor executor, ServiceBusConfiguration config)
        {
            _account = account;
            _topicName = topicName;
            _subscriptionName = subscriptionName;
            _executor = executor;
            _config = config;
        }

        public Task<IListener> CreateAsync(CancellationToken cancellationToken)
        {
            string entityPath = EntityNameHelper.FormatSubscriptionPath(_topicName, _subscriptionName);

            ServiceBusTriggerExecutor triggerExecutor = new ServiceBusTriggerExecutor(_executor);
            var listener = new ServiceBusListener(entityPath, triggerExecutor, _config, _account);

            return Task.FromResult<IListener>(listener);
        }
    }
}
