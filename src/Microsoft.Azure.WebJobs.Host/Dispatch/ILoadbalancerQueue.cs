// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Service for queues used to loadbalance across instances. 
    /// Implementation determines the storage account. 
    /// </summary>
    public interface ILoadBalancerQueue
    {
        IAsyncCollector<T> GetQueueWriter<T>(string queueName);

        IListener CreateQueueListener(string queueName, string poisonQueueName,
            Func<string, CancellationToken, Task<FunctionResult>> callback);
    }

    public class InMemoryLoadBalancerQueue : ILoadBalancerQueue
    {
        private ConcurrentDictionary<string, BufferBlock<string>> _queues = new ConcurrentDictionary<string, BufferBlock<string>>();

        public IListener CreateQueueListener(string queueName, string poisonQueueName, Func<string, CancellationToken, Task<FunctionResult>> callback)
        {
            // InMemoryLoadBalancerQueue does nothing with poisonQueueName

            var queue = _queues.GetOrAdd(queueName, CreateQueue);
            return new InMemoryQueueListener(queue, null, callback);
        }

        private BufferBlock<string> CreateQueue(string queuName)
        {
            return new BufferBlock<string>();
        }

        public IAsyncCollector<T> GetQueueWriter<T>(string queueName)
        {
            var queue = _queues.GetOrAdd(queueName, CreateQueue);
            return new InMemoryQueueWriter<T>(queue);
        }

        private class InMemoryQueueListener : IListener
        {
            private readonly BufferBlock<string> _queue;
            private readonly ActionBlock<string> _invoker;
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();

            public InMemoryQueueListener(BufferBlock<string> queue, BufferBlock<string> poisonQueue,
                Func<string, CancellationToken, Task<FunctionResult>> callback)
            {
                _queue = queue;

                // map the queue to an ActionBlock, which will execute the callback
                _invoker = new ActionBlock<string>(async s =>
                {
                    await callback(s, _cts.Token);
                });
            }

            public void Cancel()
            {
                Task.Run(async () => await StopAsync(CancellationToken.None)).GetAwaiter().GetResult();
            }

            public void Dispose()
            {
                _cts.Dispose();
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                _queue.LinkTo(_invoker, new DataflowLinkOptions { PropagateCompletion = true });
                return Task.CompletedTask;
            }

            public async Task StopAsync(CancellationToken cancellationToken)
            {
                _queue.Complete();
                _cts.Cancel();

                await _invoker.Completion;
            }
        }

        private class InMemoryQueueWriter<T> : IAsyncCollector<T>
        {
            private readonly BufferBlock<string> _queue;

            public InMemoryQueueWriter(BufferBlock<string> queue)
            {
                _queue = queue;
            }

            public async Task AddAsync(T item, CancellationToken cancellationToken = default(CancellationToken))
            {
                string serializedItem = JsonConvert.SerializeObject(item, JsonSerialization.Settings);
                await _queue.SendAsync(serializedItem);
            }

            public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                return Task.CompletedTask;
            }
        }
    }
}
