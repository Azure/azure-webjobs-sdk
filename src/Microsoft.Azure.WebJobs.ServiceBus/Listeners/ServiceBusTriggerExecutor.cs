// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.Azure.WebJobs.ServiceBus.Listeners
{
    internal class ServiceBusTriggerExecutor : ITriggerExecutor<BrokeredMessage>
    {
        private readonly ITriggeredFunctionExecutor _innerExecutor;

        public ServiceBusTriggerExecutor(ITriggeredFunctionExecutor innerExecutor)
        {
            _innerExecutor = innerExecutor;
        }

        public async Task<bool> ExecuteAsync(BrokeredMessage value, CancellationToken cancellationToken)
        {
            Guid? parentId = ServiceBusCausalityHelper.GetOwner(value);
            return await _innerExecutor.TryExecuteAsync(parentId, value, cancellationToken);
        }
    }
}
