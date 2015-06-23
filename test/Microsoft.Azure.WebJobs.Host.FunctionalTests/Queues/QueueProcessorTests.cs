﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Host.Storage.Queue;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests
{
    public class QueueProcessorTests : IClassFixture<QueueProcessorTests.TestFixture>
    {
        private CloudQueue _queue;
        private CloudQueue _poisonQueue;
        private QueueProcessor _processor;
        private TextWriter _log;
        private JobHostQueuesConfiguration _queuesConfig;

        public QueueProcessorTests(TestFixture fixture)
        {
            _log = new StringWriter();
            _queue = fixture.Queue;
            _poisonQueue = fixture.PoisonQueue;

            _queuesConfig = new JobHostQueuesConfiguration();
            QueueProcessorFactoryContext context = new QueueProcessorFactoryContext(_queue, _log, _queuesConfig);
            _processor = new QueueProcessor(context);
        }

        [Fact]
        public void Constructor_DefaultsValues()
        {
            QueueProcessorFactoryContext context = new QueueProcessorFactoryContext(_queue, _log, _queuesConfig);
            QueueProcessor localProcessor = new QueueProcessor(context);
            Assert.Equal(_queuesConfig.BatchSize, localProcessor.BatchSize);
            Assert.Equal(_queuesConfig.NewBatchThreshold, localProcessor.NewBatchThreshold);
        }

        [Fact]
        public async Task CompleteProcessingMessageAsync_Success_DeletesMessage()
        {
            CloudQueueMessage message = new CloudQueueMessage("Test Message");
            await _queue.AddMessageAsync(message, CancellationToken.None);

            message = _queue.GetMessage();

            FunctionResult result = new FunctionResult(true);
            await _processor.CompleteProcessingMessageAsync(message, result, CancellationToken.None);

            message = await _queue.GetMessageAsync();
            Assert.Null(message);
        }

        [Fact]
        public async Task CompleteProcessingMessageAsync_FailureWithoutPoisonQueue_DoesNotDeleteMessage()
        {
            CloudQueueMessage message = new CloudQueueMessage("Test Message");
            await _queue.AddMessageAsync(message, CancellationToken.None);

            message = _queue.GetMessage();
            string id = message.Id;

            FunctionResult result = new FunctionResult(false);
            await _processor.CompleteProcessingMessageAsync(message, result, CancellationToken.None);

            // make the message visible again so we can verify it wasn't deleted
            await _queue.UpdateMessageAsync(message, TimeSpan.Zero, MessageUpdateFields.Visibility);

            message = await _queue.GetMessageAsync();
            Assert.NotNull(message);
            Assert.Equal(id, message.Id);
        }

        [Fact]
        public async Task CompleteProcessingMessageAsync_MaxDequeueCountExceeded_MovesMessageToPoisonQueue()
        {
            QueueProcessorFactoryContext context = new QueueProcessorFactoryContext(_queue, _log, _queuesConfig, _poisonQueue);
            QueueProcessor localProcessor = new QueueProcessor(context);

            bool poisonMessageHandlerCalled = false;
            localProcessor.MessageAddedToPoisonQueue += (sender, e) =>
                {
                    Assert.Same(sender, localProcessor);
                    poisonMessageHandlerCalled = true;
                };

            string messageContent = Guid.NewGuid().ToString();
            CloudQueueMessage message = new CloudQueueMessage(messageContent);
            await _queue.AddMessageAsync(message, CancellationToken.None);

            FunctionResult result = new FunctionResult(false);
            for (int i = 0; i < context.MaxDequeueCount; i++)
            {
                message = await _queue.GetMessageAsync();
                await localProcessor.CompleteProcessingMessageAsync(message, result, CancellationToken.None);
            }

            message = await _queue.GetMessageAsync();
            Assert.Null(message);

            CloudQueueMessage poisonMessage = await _poisonQueue.GetMessageAsync();
            Assert.NotNull(poisonMessage);
            Assert.Equal(messageContent, poisonMessage.AsString);
            Assert.True(poisonMessageHandlerCalled);
        }

        public class TestFixture : IDisposable
        {
            private const string TestQueuePrefix = "queueprocessortests";

            public TestFixture()
            {
                DefaultStorageAccountProvider accountProvider = new DefaultStorageAccountProvider();
                var task = accountProvider.GetStorageAccountAsync(CancellationToken.None);
                task.Wait();
                IStorageQueueClient client = task.Result.CreateQueueClient();
                QueueClient = client.SdkObject;

                string queueName = string.Format("{0}-{1}", TestQueuePrefix, Guid.NewGuid());
                Queue = client.GetQueueReference(queueName).SdkObject;
                Queue.CreateIfNotExistsAsync(CancellationToken.None).Wait();

                string poisonQueueName = string.Format("{0}-poison", queueName);
                PoisonQueue = client.GetQueueReference(poisonQueueName).SdkObject;
                PoisonQueue.CreateIfNotExistsAsync(CancellationToken.None).Wait();
            }

            public CloudQueue Queue
            {
                get;
                private set;
            }

            public CloudQueue PoisonQueue
            {
                get;
                private set;
            }

            public CloudQueueClient QueueClient
            {
                get;
                private set;
            }

            public void Dispose()
            {
                foreach (var queue in QueueClient.ListQueues(TestQueuePrefix))
                {
                    queue.Delete();
                }
            }
        }
    }
}
