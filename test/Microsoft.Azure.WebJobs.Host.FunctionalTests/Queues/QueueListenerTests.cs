// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.


using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Queues.Listeners;
using Microsoft.Azure.WebJobs.Host.Storage.Queue;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.WindowsAzure.Storage.Queue;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests.Queues
{
    public class QueueListenerTests : IClassFixture<QueueListenerTests.TestFixture>
    {
        private TraceWriter _trace;
        private CloudQueue _queue;
        private CloudQueue _poisonQueue;

        private JobHostQueuesConfiguration _queuesConfig;

        private StorageQueue _storageQueue;
        private StorageQueue _storagePoisonQueue;
        private Mock<ITriggerExecutor<IStorageQueueMessage>> _mockTriggerExecutor = new Mock<ITriggerExecutor<IStorageQueueMessage>>(MockBehavior.Strict);

        public QueueListenerTests(TestFixture fixture)
        {
            _trace = new TestTraceWriter(TraceLevel.Verbose);
            _queue = fixture.CreateNewQueue();
            _poisonQueue = fixture.CreateNewQueue();
            _queuesConfig = new JobHostQueuesConfiguration { MaxDequeueCount = 2 };
            _storageQueue = new StorageQueue(new StorageQueueClient(fixture.QueueClient), _queue);
            _storagePoisonQueue = new StorageQueue(new StorageQueueClient(fixture.QueueClient), _poisonQueue);
            _mockTriggerExecutor
                .Setup(m => m.ExecuteAsync(It.IsAny<IStorageQueueMessage>(), CancellationToken.None))
                .ReturnsAsync(new FunctionResult(false));
            
        }

        [Fact]
        public async Task UpdatedQueueMessage_RetainsOriginalProperties()
        {
            string messageContent = Guid.NewGuid().ToString();
            CloudQueueMessage message = new CloudQueueMessage(messageContent);
            await _queue.AddMessageAsync(message, CancellationToken.None);
            CloudQueueMessage messageFromCloud = await _queue.GetMessageAsync();

            QueueListener listener = new QueueListener(_storageQueue, _storagePoisonQueue, _mockTriggerExecutor.Object, new WebJobsExceptionHandler(), _trace,
                null, null, _queuesConfig);

            await listener.ProcessMessageAsync(new StorageQueueMessage(messageFromCloud), TimeSpan.FromMinutes(10), CancellationToken.None);

            // pull the message and process it again (to have it go through the poison _queue flow)
            messageFromCloud = await _queue.GetMessageAsync();
            Assert.Equal(2, messageFromCloud.DequeueCount);

            await listener.ProcessMessageAsync(new StorageQueueMessage(messageFromCloud), TimeSpan.FromMinutes(10), CancellationToken.None);

            // Make sure the message was processed and deleted.
            await _queue.FetchAttributesAsync();
            Assert.Equal(0, _queue.ApproximateMessageCount);

            // The Listener has inserted a message to the poison _queue.
            await _poisonQueue.FetchAttributesAsync();
            Assert.Equal(1, _poisonQueue.ApproximateMessageCount);

            _mockTriggerExecutor.Verify(m => m.ExecuteAsync(It.IsAny<IStorageQueueMessage>(), CancellationToken.None), Times.Exactly(2));
        }

        [Fact]
        public async Task RenewedQueueMessage_DeletesCorrectly()
        {
            string messageContent = Guid.NewGuid().ToString();
            CloudQueueMessage message = new CloudQueueMessage(messageContent);
            await _queue.AddMessageAsync(message, CancellationToken.None);
            CloudQueueMessage messageFromCloud = await _queue.GetMessageAsync();

            QueueListener listener = new QueueListener(_storageQueue, null, _mockTriggerExecutor.Object, new WebJobsExceptionHandler(), _trace,
                null, null, new JobHostQueuesConfiguration());
            listener.MinimumVisibilityRenewalInterval = TimeSpan.FromSeconds(1);

            // Set up a function that sleeps to allow renewal
            _mockTriggerExecutor
                .Setup(m => m.ExecuteAsync(It.Is<IStorageQueueMessage>(msg => msg.DequeueCount == 1), CancellationToken.None))
                .ReturnsAsync(() =>
                {
                    Thread.Sleep(4000);
                    return new FunctionResult(true);
                });

            var previousNextVisibleTime = messageFromCloud.NextVisibleTime;
            var previousPopReceipt = messageFromCloud.PopReceipt;

            // Renewal should happen at 2 seconds
            await listener.ProcessMessageAsync(new StorageQueueMessage(messageFromCloud), TimeSpan.FromSeconds(4), CancellationToken.None);

            // Check to make sure the renewal occurred.
            Assert.NotEqual(messageFromCloud.NextVisibleTime, previousNextVisibleTime);
            Assert.NotEqual(messageFromCloud.PopReceipt, previousPopReceipt);

            // Make sure the message was processed and deleted.
            await _queue.FetchAttributesAsync();
            Assert.Equal(0, _queue.ApproximateMessageCount);
        }

        [Fact]
        public async Task MaxDequeueCountExceeded_MoveMessageToPoisonQueue()
        {
            string messageContent = Guid.NewGuid().ToString();
            CloudQueueMessage message = new CloudQueueMessage(messageContent);
            await _queue.AddMessageAsync(message, CancellationToken.None);
            CloudQueueMessage messageFromCloud = await _queue.GetMessageAsync();

            QueueListener listener = new QueueListener(_storageQueue, _storagePoisonQueue, _mockTriggerExecutor.Object, new WebJobsExceptionHandler(), _trace,
                null, null, _queuesConfig);

            await listener.ProcessMessageAsync(new StorageQueueMessage(messageFromCloud), TimeSpan.FromMinutes(10), CancellationToken.None);

            // pull the message and process it again (to have it go through the poison _queue flow)
            messageFromCloud = await _queue.GetMessageAsync();
            Assert.Equal(2, messageFromCloud.DequeueCount);
            //Put message back in the _queue and read it to increase dequeuecount
            TimeSpan visibilityTimeout = TimeSpan.FromMilliseconds(0);
            await _queue.UpdateMessageAsync(messageFromCloud, visibilityTimeout, MessageUpdateFields.Visibility, CancellationToken.None);
            messageFromCloud = await _queue.GetMessageAsync();
            Assert.Equal(3, messageFromCloud.DequeueCount);

            await listener.ProcessMessageAsync(new StorageQueueMessage(messageFromCloud), TimeSpan.FromMinutes(10), CancellationToken.None);

            // Make sure the message was processed and deleted.
            await _queue.FetchAttributesAsync();
            Assert.Equal(0, _queue.ApproximateMessageCount);

            // The Listener has inserted a message to the poison _queue.
            await _poisonQueue.FetchAttributesAsync();
            Assert.Equal(1, _poisonQueue.ApproximateMessageCount);

            _mockTriggerExecutor.Verify(m => m.ExecuteAsync(It.IsAny<IStorageQueueMessage>(), CancellationToken.None), Times.Exactly(1));
        }
        public class TestFixture : IDisposable
        {
            private const string TestQueuePrefix = "queuelistenertests";

            public TestFixture()
            {
                Mock<IServiceProvider> services = new Mock<IServiceProvider>(MockBehavior.Strict);
                StorageClientFactory clientFactory = new StorageClientFactory();
                services.Setup(p => p.GetService(typeof(StorageClientFactory))).Returns(clientFactory);

                DefaultStorageAccountProvider accountProvider = new DefaultStorageAccountProvider(services.Object);
                var task = accountProvider.GetStorageAccountAsync(CancellationToken.None);
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

            public CloudQueue CreateNewQueue()
            {
                string queueName = string.Format("{0}-{1}", TestQueuePrefix, Guid.NewGuid());
                var _queue = QueueClient.GetQueueReference(queueName);
                _queue.CreateIfNotExistsAsync(CancellationToken.None).Wait();
                return _queue;
            }

            public void Dispose()
            {
                foreach (var _queue in QueueClient.ListQueues(TestQueuePrefix))
                {
                    _queue.Delete();
                }
            }
        }

    }
}
