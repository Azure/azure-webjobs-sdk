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
        private readonly bool _singleDispatch;

        public ServiceBusListenerFactory(ServiceBusAccount account, ITriggeredFunctionExecutor executor, ServiceBusOptions options, MessagingProvider messagingProvider, bool singleDispatch)
        {
            _account = account;
            _executor = executor;
            _options = options;
            _messagingProvider = messagingProvider;
            _singleDispatch = singleDispatch;
        }

        public Task<IListener> CreateAsync(CancellationToken cancellationToken)
        {
            var listener = new ServiceBusListener(_executor, _options, _account, _messagingProvider, _singleDispatch);
            return Task.FromResult<IListener>(listener);
        }
    }
}
