﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Queue;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests.TestDoubles
{
    internal class FakeQueuesOptionsFactory : IOptionsFactory<JobHostQueuesOptions>
    {
        private readonly FakeQueueProcessorFactory _queueProcessorFactory;

        public FakeQueuesOptionsFactory(IStorageAccountProvider accountProvider)
        {
            _queueProcessorFactory = new FakeQueueProcessorFactory(accountProvider);
        }

        public JobHostQueuesOptions Create(string name)
        {
            return new JobHostQueuesOptions
            {
                BatchSize = 2,
                MaxPollingInterval = TimeSpan.FromSeconds(10),
                MaxDequeueCount = 3,
                VisibilityTimeout = TimeSpan.Zero,
                QueueProcessorFactory = _queueProcessorFactory
            };
        }

        private class FakeQueueProcessorFactory : DefaultQueueProcessorFactory
        {
            private IStorageAccount _storageAccount;

            public FakeQueueProcessorFactory(IStorageAccountProvider accountProvider)
            {
                CancellationToken token = new CancellationToken();
                Task<IStorageAccount> task = accountProvider.GetStorageAccountAsync(token);
                _storageAccount = task.Result;
            }

            public override QueueProcessor Create(QueueProcessorFactoryContext context)
            {
                return new FakeQueueProcessor(context, _storageAccount);
            }
        }

        /// <summary>
        /// This QueueProcessor mocks out the queue storage operations by mapping back from
        /// CloudQueueMessage to the fake message instances, and using the mock IStorageQueue
        /// implementations.
        /// </summary>
        private class FakeQueueProcessor : QueueProcessor
        {
            private IStorageQueue _queue;
            private IStorageQueue _poisonQueue;

            public FakeQueueProcessor(QueueProcessorFactoryContext context, IStorageAccount storageAccount) :
                base(context)
            {
                // map the queue names from the context to fake queues
                IStorageQueueClient client = storageAccount.CreateQueueClient();
                _queue = client.GetQueueReference(context.Queue.Name);
                if (context.PoisonQueue != null)
                {
                    _poisonQueue = client.GetQueueReference(context.PoisonQueue.Name);
                }
            }

            protected override async Task CopyMessageToPoisonQueueAsync(CloudQueueMessage message, CloudQueue poisonQueue, CancellationToken cancellationToken)
            {
                FakeStorageQueueMessage fakeMessage = new FakeStorageQueueMessage(message);

                await _poisonQueue.CreateIfNotExistsAsync(cancellationToken);
                await _poisonQueue.AddMessageAsync(fakeMessage, cancellationToken);

                OnMessageAddedToPoisonQueue(new PoisonMessageEventArgs(message, poisonQueue));
            }

            protected override async Task ReleaseMessageAsync(CloudQueueMessage message, FunctionResult result, TimeSpan visibilityTimeout, CancellationToken cancellationToken)
            {
                FakeStorageQueueMessage fakeMessage = new FakeStorageQueueMessage(message);

                await _queue.UpdateMessageAsync(fakeMessage, visibilityTimeout, MessageUpdateFields.Visibility, cancellationToken);
            }

            protected override async Task DeleteMessageAsync(CloudQueueMessage message, CancellationToken cancellationToken)
            {
                FakeStorageQueueMessage fakeMessage = new FakeStorageQueueMessage(message);
                await _queue.DeleteMessageAsync(fakeMessage, cancellationToken);
            }
        }
    }
}
