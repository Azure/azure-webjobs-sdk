// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Host.Queues.Listeners;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace WebJobs.Extensions.Storage
{
    // $$$ This exposes Azure Storage implementations for runtime state objects. 
    internal class StorageLoadBalancerQueue : ILoadBalancerQueue
    {
        private readonly JobHostQueuesOptions _queueOptions;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IWebJobsExceptionHandler _exceptionHandler;
        private readonly SharedQueueWatcher _sharedWatcher;
        private readonly StorageAccountProvider _storageAccountProvider;

        public StorageLoadBalancerQueue(
            StorageAccountProvider storageAccountProvider,
            IOptions<JobHostQueuesOptions> queueOptions,
            IWebJobsExceptionHandler exceptionHandler,
            SharedQueueWatcher sharedWatcher,
            ILoggerFactory loggerFactory)
        {
            _storageAccountProvider = storageAccountProvider;
            _queueOptions = queueOptions.Value;
            _exceptionHandler = exceptionHandler;
            _sharedWatcher = sharedWatcher;
            _loggerFactory = loggerFactory;
        }

        private CloudQueue CreateQueue(string queueName)
        {
            var account = _storageAccountProvider.GetHost();
            return account.CreateCloudQueueClient().GetQueueReference(queueName);
        }

        public IAsyncCollector<T> GetQueueWriter<T>(string queueName)
        {
            return new StorageLoadBalancerQueueWriter<T>(this, CreateQueue(queueName));
        }

        private class StorageLoadBalancerQueueWriter<T> : IAsyncCollector<T>
        {
            StorageLoadBalancerQueue _parent;
            CloudQueue _queue;

            public StorageLoadBalancerQueueWriter(StorageLoadBalancerQueue parent, CloudQueue queue)
            {
                _parent = parent;
                _queue = queue;
            }

            public async Task AddAsync(T item, CancellationToken cancellationToken = default(CancellationToken))
            {
                string contents = JsonConvert.SerializeObject(item, JsonSerialization.Settings);

                var msg = new CloudQueueMessage(contents);
                await _queue.AddMessageAndCreateIfNotExistsAsync(msg, cancellationToken);

                _parent._sharedWatcher.Notify(_queue.Name);
            }

            public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                return Task.CompletedTask;
            }
        }

        public IListener CreateQueueListener(string queue, string poisonQueue,
            Func<string, CancellationToken, Task<FunctionResult>> callback)
        {
            // Provide an upper bound on the maximum polling interval for run/abort from dashboard.
            // This ensures that if users have customized this value the Dashboard will remain responsive.
            TimeSpan maxPollingInterval = QueuePollingIntervals.DefaultMaximum;

            var wrapper = new StorageLoadBalancerQueueTriggerExecutor(callback);

            IListener listener = new QueueListener(CreateQueue(queue),
                poisonQueue: CreateQueue(poisonQueue),
                triggerExecutor: wrapper,
                exceptionHandler: _exceptionHandler,
                loggerFactory: _loggerFactory,
                sharedWatcher: _sharedWatcher,
                queueOptions: _queueOptions,
                maxPollingInterval: maxPollingInterval);

            return listener;
        }

        private class StorageLoadBalancerQueueTriggerExecutor : ITriggerExecutor<CloudQueueMessage>
        {
            private readonly Func<string, CancellationToken, Task<FunctionResult>> _callback;

            public StorageLoadBalancerQueueTriggerExecutor(Func<string, CancellationToken, Task<FunctionResult>> callback)
            {
                _callback = callback;
            }

            public Task<FunctionResult> ExecuteAsync(CloudQueueMessage value, CancellationToken cancellationToken)
            {
                return _callback(value.AsString, cancellationToken);
            }
        }
    }
}
