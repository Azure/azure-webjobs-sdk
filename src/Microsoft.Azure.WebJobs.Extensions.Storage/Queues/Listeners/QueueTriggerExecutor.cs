// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Microsoft.Azure.WebJobs.Host.Queues.Listeners
{
    internal class QueueTriggerExecutor : ITriggerExecutor<CloudQueueMessage>
    {
        private readonly ITriggeredFunctionExecutor _innerExecutor;

        public QueueTriggerExecutor(ITriggeredFunctionExecutor innerExecutor)
        {
            _innerExecutor = innerExecutor;
        }

        public async Task<FunctionResult> ExecuteAsync(CloudQueueMessage value, CancellationToken cancellationToken)
        {
            Guid? parentId = QueueCausalityManager.GetOwner(value);
            TriggeredFunctionData input = new TriggeredFunctionData
            {
                ParentId = parentId,
                TriggerValue = value, 
                TriggerDetails = FormatTriggerDetails(value)
            };
            return await _innerExecutor.TryExecuteAsync(input, cancellationToken);
        }

        private string FormatTriggerDetails(CloudQueueMessage value)
        {
            return $"MessageId: {value.Id}, " +
            $"DequeueCount: {value.DequeueCount}, " +
            $"InsertionTime: {value.InsertionTime}";
        }
    }
}
