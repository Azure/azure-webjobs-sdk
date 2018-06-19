// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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

    public class InMemoryLoadbalancerQueue : ILoadbalancerQueue
    {
        public IListener CreateQueueListenr(string queue, string poisonQueue, Func<string, CancellationToken, Task<FunctionResult>> callback)
        {
            return new Listener
            {
                _parent = this,
                queue = queue,
                callback = callback
            };
        }

        Dictionary<string, Queue<object>> _queues = new Dictionary<string, Queue<object>>();

        private Queue<object> GetQueue(string name)
        {
            lock (_queues)
            {
                Queue<object> q;
                if (!_queues.TryGetValue(name, out q))
                {
                    q = new Queue<object>();
                    _queues[name] = q;
                }
                return q;
            }
        }

        private void Add<T>(string queue, T item)
        {
            var q = GetQueue(queue);
            lock (q)
            {
                q.Enqueue(item);
            }
        }

        public IAsyncCollector<T> GetQueueWriter<T>(string queueName)
        {
            return new Writer<T>
            {
                 _parent = this,
                  _queue = queueName
            };
        }

        class Listener : IListener
        {
            internal InMemoryLoadbalancerQueue _parent;
            internal string queue; // queue to listen on
            internal Func<string, CancellationToken, Task<FunctionResult>> callback;

            public void Cancel()
            {
            }

            public void Dispose()
            {
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        class Writer<T> : IAsyncCollector<T>
        {
            internal string _queue;
            internal InMemoryLoadbalancerQueue _parent;

            public Task AddAsync(T item, CancellationToken cancellationToken = default(CancellationToken))
            {
                _parent.Add(_queue, item);
                return Task.CompletedTask;
            }

            public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                return Task.CompletedTask;
            }
        }
    }
}
