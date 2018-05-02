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

namespace WebJobs.Extension.Storage
{
    // $$$ This exposes Azure Storage implementations for runtime state objects. 
    class Class1 : ISuperhack
    {
        private readonly JobHostQueuesOptions _queueOptions;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IWebJobsExceptionHandler _exceptionHandler;
        private readonly SharedQueueWatcher _sharedWatcher;
        private readonly XStorageAccountProvider _storageAccountProvider;

        public Class1(
            XStorageAccountProvider storageAccountProvider,
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

        public QueueMoniker GetQueueReference(string queueName) // Storage accounts?
        {
            return new QueueMoniker
            {
                 // ConnectionString = _storageAccountProvider.DashboardConnectionString, $$$
                  QueueName = queueName
            };
        }

        public IAsyncCollector<T> GetQueueWriter<T>(QueueMoniker queue)
        {
            return new QueueWriter<T>(this, Convert(queue));
        }

        class QueueWriter<T> : IAsyncCollector<T>
        {
            Class1 _parent;
            CloudQueue _queue;

            public QueueWriter(Class1 parent, CloudQueue queue)
            {
                this._parent = parent;
                this._queue = queue;
            }


            public async Task AddAsync(T item, CancellationToken cancellationToken = default(CancellationToken))
            {
                string contents = JsonConvert.SerializeObject(
                    item, 
                    JsonSerialization.Settings);

                var msg = new CloudQueueMessage(contents);
                await _queue.AddMessageAndCreateIfNotExistsAsync(msg, cancellationToken);

                _parent._sharedWatcher.Notify(_queue.Name);

            }

            public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                return Task.CompletedTask;
            }
        }

        CloudQueue Convert(QueueMoniker queueMoniker)
        {
            // var account = Task.Run(() => _storageAccountProvider.GetDashboardAccountAsync(CancellationToken.None)).GetAwaiter().GetResult();
            var account = _storageAccountProvider.Get("Dashboard"); // $$$
            var queue = account.CreateCloudQueueClient().GetQueueReference(queueMoniker.QueueName);
            return queue;
        }

        public IListener CreateQueueListenr(
            QueueMoniker queue,
            QueueMoniker poisonQueue,
            Func<string, CancellationToken,  Task<FunctionResult>> callback
            )
        {
            // Provide an upper bound on the maximum polling interval for run/abort from dashboard.
            // This ensures that if users have customized this value the Dashboard will remain responsive.
            TimeSpan maxPollingInterval = QueuePollingIntervals.DefaultMaximum;

            var wrapper = new Wrapper
            {
                _callback = callback
            };

            IListener listener = new QueueListener(Convert(queue),
                poisonQueue: Convert(poisonQueue),
                triggerExecutor: wrapper,
                exceptionHandler: _exceptionHandler,
                loggerFactory: _loggerFactory,
                sharedWatcher: _sharedWatcher,
                queueOptions: _queueOptions,
                maxPollingInterval: maxPollingInterval);

            return listener;
        }

        // $$$ cleanup
        class Wrapper : ITriggerExecutor<CloudQueueMessage>
        {
            public Func<string, CancellationToken, Task<FunctionResult>> _callback;

            public Task<FunctionResult> ExecuteAsync(CloudQueueMessage value, CancellationToken cancellationToken)
            {
                return _callback(value.AsString, cancellationToken);
            }
        }
    }
}
