// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.ServiceBus;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.ServiceBus.Listeners
{
    internal class ServiceBusTriggerExecutor
    {
        private readonly ITriggeredFunctionExecutor _innerExecutor;

        public ServiceBusTriggerExecutor(ITriggeredFunctionExecutor innerExecutor)
        {
            _innerExecutor = innerExecutor;
        }

        public async Task<FunctionResult> ExecuteAsync(Message value, CancellationToken cancellationToken)
        {
            Guid? parentId = ServiceBusCausalityHelper.GetOwner(value);
            TriggeredFunctionData input = new TriggeredFunctionData
            {
                ParentId = parentId,
                TriggerValue = value,
                TriggerDetails = PopulateTriggerDetails(value)
            };
            return await _innerExecutor.TryExecuteAsync(input, cancellationToken);
        }

        private Dictionary<string, string> PopulateTriggerDetails(Message value)
        {
            return new Dictionary<string, string>()
            {
                { "MessageId", value.MessageId },
                { "DeliveryCount", value.SystemProperties.DeliveryCount.ToString() },
                { "EnqueuedTime", value.SystemProperties.EnqueuedTimeUtc.ToString() },
                { "LockedUntil", value.SystemProperties.LockedUntilUtc.ToString() }
            };
        }
    }
}
