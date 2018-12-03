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
        private readonly MessagingProvider _messagingProvider;
        private readonly string _queueName;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly ServiceBusOptions _options;

        public ServiceBusQueueListenerFactory(MessagingProvider messagingProvider, string queueName, ITriggeredFunctionExecutor executor, ServiceBusOptions options)
        {
            _messagingProvider = messagingProvider;
            _queueName = queueName;
            _executor = executor;
            _options = options;
        }

        public Task<IListener> CreateAsync(CancellationToken cancellationToken)
        {
            var triggerExecutor = new ServiceBusTriggerExecutor(_executor);
            var listener = new ServiceBusListener(_queueName, triggerExecutor, _messagingProvider);

            return Task.FromResult<IListener>(listener);
        }
    }
}
