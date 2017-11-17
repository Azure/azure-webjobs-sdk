// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.ServiceBus;

namespace Microsoft.Azure.WebJobs.ServiceBus.Listeners
{
    internal class ServiceBusQueueListenerFactory : IListenerFactory
    {
        private readonly ServiceBusAccount _account;
        private readonly string _queueName;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly ServiceBusConfiguration _config;

        public ServiceBusQueueListenerFactory(ServiceBusAccount account, string queueName, ITriggeredFunctionExecutor executor, ServiceBusConfiguration config)
        {
            _account = account;
            _queueName = queueName;
            _executor = executor;
            _config = config;
        }

        public Task<IListener> CreateAsync(CancellationToken cancellationToken)
        {
            var triggerExecutor = new ServiceBusTriggerExecutor(_executor);
            var listener = new ServiceBusListener(_queueName, triggerExecutor, _config, _account);

            return Task.FromResult<IListener>(listener);
        }
    }
}
