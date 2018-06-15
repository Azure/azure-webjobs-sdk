// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Service for queues used to loadbalance across instances. 
    /// Implementation determines the storage account. 
    /// </summary>
    public interface ILoadbalancerQueue
    {
        // Host may use queues internally for distributing work items. 
        IAsyncCollector<T> GetQueueWriter<T>(string queueName);

        IListener CreateQueueListenr(
            string queue, // queue to listen on
            string poisonQueue, // optional. Message enqueue here if callback fails
            Func<string, CancellationToken, Task<FunctionResult>> callback
            );
    }
}
