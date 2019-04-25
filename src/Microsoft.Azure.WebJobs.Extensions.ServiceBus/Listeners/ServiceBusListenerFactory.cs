// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.ServiceBus.Listeners
{
    internal class ServiceBusListenerFactory : IListenerFactory
    {
        private readonly ServiceBusAccount _account;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly ServiceBusOptions _options;
        private readonly MessagingProvider _messagingProvider;
        private readonly ServiceBusEntityInfo _entity;

        public ServiceBusListenerFactory(ServiceBusAccount account, ServiceBusEntityInfo entity, ITriggeredFunctionExecutor executor, ServiceBusOptions options, MessagingProvider messagingProvider)
        {
            _account = account;
            _entity = entity;
            _executor = executor;
            _options = options;
            _messagingProvider = messagingProvider;
        }

        public Task<IListener> CreateAsync(CancellationToken cancellationToken)
        {
            var triggerExecutor = new ServiceBusTriggerExecutor(_executor);
            var listener = new ServiceBusListener(_entity, triggerExecutor, _options, _account, _messagingProvider);

            return Task.FromResult<IListener>(listener);
        }
    }
}
