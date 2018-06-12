// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;

namespace Microsoft.Azure.WebJobs.ServiceBus.Listeners
{
    internal class ServiceBusQueueListenerFactory : IListenerFactory
    {
        private readonly ServiceBusAccount _account;
        private readonly string _queueName;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly ServiceBusOptions _config;
        private readonly IMessagingProvider _messagingProvider;

        public ServiceBusQueueListenerFactory(ServiceBusAccount account, string queueName, ITriggeredFunctionExecutor executor, ServiceBusOptions config, IMessagingProvider messagingProvider)
        {
            _account = account;
            _queueName = queueName;
            _executor = executor;
            _config = config;
            _messagingProvider = messagingProvider;
        }

        public Task<IListener> CreateAsync(CancellationToken cancellationToken)
        {
            var triggerExecutor = new ServiceBusTriggerExecutor(_executor);
            var listener = new ServiceBusListener(_queueName, triggerExecutor, _config, _account, _messagingProvider);

            return Task.FromResult<IListener>(listener);
        }
    }
}
