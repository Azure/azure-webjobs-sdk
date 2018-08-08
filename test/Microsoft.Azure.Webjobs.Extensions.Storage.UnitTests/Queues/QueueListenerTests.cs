// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Host.Queues.Listeners;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Queues
{
    public class QueueListenerTests : IClassFixture<QueueListenerTests.TestFixture>
    {
        private Mock<CloudQueue> _mockQueue;
        private QueueListener _listener;
        private Mock<QueueProcessor> _mockQueueProcessor;
        private Mock<ITriggerExecutor<CloudQueueMessage>> _mockTriggerExecutor;
        private CloudQueueMessage _queueMessage;
        private ILoggerFactory _loggerFactory;

        public QueueListenerTests(TestFixture fixture)
        {
            Fixture = fixture;

            _mockQueue = new Mock<CloudQueue>(new Uri("https://test.queue.core.windows.net/testqueue"));

            _mockTriggerExecutor = new Mock<ITriggerExecutor<CloudQueueMessage>>(MockBehavior.Strict);
            Mock<IWebJobsExceptionHandler> mockExceptionDispatcher = new Mock<IWebJobsExceptionHandler>(MockBehavior.Strict);
            _loggerFactory = new LoggerFactory();
            _loggerFactory.AddProvider(new TestLoggerProvider());
            Mock<IQueueProcessorFactory> mockQueueProcessorFactory = new Mock<IQueueProcessorFactory>(MockBehavior.Strict);
            JobHostQueuesOptions queuesConfig = new JobHostQueuesOptions();
            QueueProcessorFactoryContext context = new QueueProcessorFactoryContext(_mockQueue.Object, _loggerFactory, queuesConfig);

            _mockQueueProcessor = new Mock<QueueProcessor>(MockBehavior.Strict, context);
            JobHostQueuesOptions queueConfig = new JobHostQueuesOptions
            {
                MaxDequeueCount = 5,
                QueueProcessorFactory = mockQueueProcessorFactory.Object
            };

            mockQueueProcessorFactory.Setup(p => p.Create(It.IsAny<QueueProcessorFactoryContext>())).Returns(_mockQueueProcessor.Object);

            _listener = new QueueListener(_mockQueue.Object, null, _mockTriggerExecutor.Object, mockExceptionDispatcher.Object, _loggerFactory, null, queueConfig);
            _queueMessage = new CloudQueueMessage("TestMessage");
        }

        public TestFixture Fixture { get; set; }

        [Fact]
        public async Task UpdatedQueueMessage_RetainsOriginalProperties()
        {
            CloudQueue queue = Fixture.CreateNewQueue();
            CloudQueue poisonQueue = Fixture.CreateNewQueue();

            var queuesConfig = new JobHostQueuesOptions { MaxDequeueCount = 2 };

            Mock<ITriggerExecutor<CloudQueueMessage>> mockTriggerExecutor = new Mock<ITriggerExecutor<CloudQueueMessage>>(MockBehavior.Strict);

            string messageContent = Guid.NewGuid().ToString();
            CloudQueueMessage message = new CloudQueueMessage(messageContent);
            await queue.AddMessageAsync(message, null, null, null, null, CancellationToken.None);
            CloudQueueMessage messageFromCloud = await queue.GetMessageAsync();

            QueueListener listener = new QueueListener(queue, poisonQueue, mockTriggerExecutor.Object, new WebJobsExceptionHandler(null),
                null, null, queuesConfig);

            mockTriggerExecutor
                .Setup(m => m.ExecuteAsync(It.IsAny<CloudQueueMessage>(), CancellationToken.None))
                .ReturnsAsync(new FunctionResult(false));

            await listener.ProcessMessageAsync(messageFromCloud, TimeSpan.FromMinutes(10), CancellationToken.None);

            // pull the message and process it again (to have it go through the poison queue flow)
            messageFromCloud = await queue.GetMessageAsync();
            Assert.Equal(2, messageFromCloud.DequeueCount);

            await listener.ProcessMessageAsync(messageFromCloud, TimeSpan.FromMinutes(10), CancellationToken.None);

            // Make sure the message was processed and deleted.
            await queue.FetchAttributesAsync();
            Assert.Equal(0, queue.ApproximateMessageCount);

            // The Listener has inserted a message to the poison queue.
            await poisonQueue.FetchAttributesAsync();
            Assert.Equal(1, poisonQueue.ApproximateMessageCount);

            mockTriggerExecutor.Verify(m => m.ExecuteAsync(It.IsAny<CloudQueueMessage>(), CancellationToken.None), Times.Exactly(2));
        }

        [Fact]
        public async Task RenewedQueueMessage_DeletesCorrectly()
        {
            CloudQueue queue = Fixture.CreateNewQueue();

            Mock<ITriggerExecutor<CloudQueueMessage>> mockTriggerExecutor = new Mock<ITriggerExecutor<CloudQueueMessage>>(MockBehavior.Strict);

            string messageContent = Guid.NewGuid().ToString();
            CloudQueueMessage message = new CloudQueueMessage(messageContent);
            await queue.AddMessageAsync(message, null, null, null, null, CancellationToken.None);
            CloudQueueMessage messageFromCloud = await queue.GetMessageAsync();

            QueueListener listener = new QueueListener(queue, null, mockTriggerExecutor.Object, new WebJobsExceptionHandler(null),
                null, null, new JobHostQueuesOptions());
            listener.MinimumVisibilityRenewalInterval = TimeSpan.FromSeconds(1);

            // Set up a function that sleeps to allow renewal
            mockTriggerExecutor
                .Setup(m => m.ExecuteAsync(It.Is<CloudQueueMessage>(msg => msg.DequeueCount == 1), CancellationToken.None))
                .ReturnsAsync(() =>
                {
                    Thread.Sleep(4000);
                    return new FunctionResult(true);
                });

            var previousNextVisibleTime = messageFromCloud.NextVisibleTime;
            var previousPopReceipt = messageFromCloud.PopReceipt;

            // Renewal should happen at 2 seconds
            await listener.ProcessMessageAsync(messageFromCloud, TimeSpan.FromSeconds(4), CancellationToken.None);

            // Check to make sure the renewal occurred.
            Assert.NotEqual(messageFromCloud.NextVisibleTime, previousNextVisibleTime);
            Assert.NotEqual(messageFromCloud.PopReceipt, previousPopReceipt);

            // Make sure the message was processed and deleted.
            await queue.FetchAttributesAsync();
            Assert.Equal(0, queue.ApproximateMessageCount);
        }

        [Fact]
        public void CreateQueueProcessor_CreatesProcessorCorrectly()
        {
            CloudQueue poisonQueue = null;
            bool poisonMessageHandlerInvoked = false;
            EventHandler<PoisonMessageEventArgs> poisonMessageEventHandler = (sender, e) => { poisonMessageHandlerInvoked = true; };
            Mock<IQueueProcessorFactory> mockQueueProcessorFactory = new Mock<IQueueProcessorFactory>(MockBehavior.Strict);
            JobHostQueuesOptions queueConfig = new JobHostQueuesOptions
            {
                MaxDequeueCount = 7,
                QueueProcessorFactory = mockQueueProcessorFactory.Object
            };
            QueueProcessor expectedQueueProcessor = null;
            bool processorFactoryInvoked = false;

            // create for a host queue - don't expect custom factory to be invoked
            CloudQueue queue = new CloudQueue(new Uri(string.Format("https://test.queue.core.windows.net/{0}", HostQueueNames.GetHostQueueName("12345"))));
            QueueProcessor queueProcessor = QueueListener.CreateQueueProcessor(queue, poisonQueue, _loggerFactory, queueConfig, poisonMessageEventHandler);
            Assert.False(processorFactoryInvoked);
            Assert.NotSame(expectedQueueProcessor, queueProcessor);
            queueProcessor.OnMessageAddedToPoisonQueue(new PoisonMessageEventArgs(null, poisonQueue));
            Assert.True(poisonMessageHandlerInvoked);

            QueueProcessorFactoryContext processorFactoryContext = null;
            mockQueueProcessorFactory.Setup(p => p.Create(It.IsAny<QueueProcessorFactoryContext>()))
                .Callback<QueueProcessorFactoryContext>((mockProcessorContext) =>
                {
                    processorFactoryInvoked = true;

                    Assert.Same(queue, mockProcessorContext.Queue);
                    Assert.Same(poisonQueue, mockProcessorContext.PoisonQueue);
                    Assert.Equal(queueConfig.MaxDequeueCount, mockProcessorContext.MaxDequeueCount);
                    Assert.NotNull(mockProcessorContext.Logger);

                    processorFactoryContext = mockProcessorContext;
                })
                .Returns(() =>
                {
                    expectedQueueProcessor = new QueueProcessor(processorFactoryContext);
                    return expectedQueueProcessor;
                });

            // when storage host is "localhost" we invoke the processor factory even for
            // host queues (this enables local test mocking)
            processorFactoryInvoked = false;
            queue = new CloudQueue(new Uri(string.Format("https://localhost/{0}", HostQueueNames.GetHostQueueName("12345"))));
            queueProcessor = QueueListener.CreateQueueProcessor(queue, poisonQueue, _loggerFactory, queueConfig, poisonMessageEventHandler);
            Assert.True(processorFactoryInvoked);
            Assert.Same(expectedQueueProcessor, queueProcessor);

            // create for application queue - expect processor factory to be invoked
            poisonMessageHandlerInvoked = false;
            processorFactoryInvoked = false;
            queue = new CloudQueue(new Uri("https://test.queue.core.windows.net/testqueue"));
            queueProcessor = QueueListener.CreateQueueProcessor(queue, poisonQueue, _loggerFactory, queueConfig, poisonMessageEventHandler);
            Assert.True(processorFactoryInvoked);
            Assert.Same(expectedQueueProcessor, queueProcessor);
            queueProcessor.OnMessageAddedToPoisonQueue(new PoisonMessageEventArgs(null, poisonQueue));
            Assert.True(poisonMessageHandlerInvoked);

            // if poison message watcher not specified, event not subscribed to
            poisonMessageHandlerInvoked = false;
            processorFactoryInvoked = false;
            queueProcessor = QueueListener.CreateQueueProcessor(queue, poisonQueue, _loggerFactory, queueConfig, null);
            Assert.True(processorFactoryInvoked);
            Assert.Same(expectedQueueProcessor, queueProcessor);
            queueProcessor.OnMessageAddedToPoisonQueue(new PoisonMessageEventArgs(null, poisonQueue));
            Assert.False(poisonMessageHandlerInvoked);
        }

        [Fact]
        public async Task ProcessMessageAsync_Success()
        {
            CancellationToken cancellationToken = new CancellationToken();
            FunctionResult result = new FunctionResult(true);
            _mockQueueProcessor.Setup(p => p.BeginProcessingMessageAsync(_queueMessage, cancellationToken)).ReturnsAsync(true);
            _mockTriggerExecutor.Setup(p => p.ExecuteAsync(_queueMessage, cancellationToken)).ReturnsAsync(result);
            _mockQueueProcessor.Setup(p => p.CompleteProcessingMessageAsync(_queueMessage, result, cancellationToken)).Returns(Task.FromResult(true));

            await _listener.ProcessMessageAsync(_queueMessage, TimeSpan.FromMinutes(10), cancellationToken);
        }

        [Fact]
        public async Task GetMessages_QueueCheckThrowsTransientError_ReturnsBackoffResult()
        {
            CancellationToken cancellationToken = new CancellationToken();
            var exception = new StorageException(
                new RequestResult
                {
                    HttpStatusCode = 503
                },
                string.Empty,
                new Exception());

            _mockQueue.Setup(p => p.GetMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), null, null, cancellationToken)).Throws(exception);

            var result = await _listener.ExecuteAsync(cancellationToken);
            Assert.NotNull(result);
            await result.Wait;
        }

        [Fact]
        public async Task GetMessages_ChecksQueueExistence_UntilQueueExists()
        {
            var cancellationToken = new CancellationToken();
            bool queueExists = false;
            _mockQueue.Setup(p => p.ExistsAsync()).ReturnsAsync(() => queueExists);
            _mockQueue.Setup(p => p.GetMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), null, null, cancellationToken)).ReturnsAsync(Enumerable.Empty<CloudQueueMessage>());

            int numIterations = 5;
            int numFailedExistenceChecks = 2;
            for (int i = 0; i < numIterations; i++)
            {
                if (i >= numFailedExistenceChecks)
                {
                    // after the second failed check, simulate the queue
                    // being added
                    queueExists = true;
                }

                var result = await _listener.ExecuteAsync(cancellationToken);
                await result.Wait;
            }

            _mockQueue.Verify(p => p.ExistsAsync(), Times.Exactly(numIterations - numFailedExistenceChecks));
            _mockQueue.Verify(p => p.GetMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), null, null, cancellationToken), Times.Exactly(numIterations - numFailedExistenceChecks));
        }

        [Fact]
        public async Task GetMessages_ResetsQueueExistenceCheck_OnException()
        {
            var cancellationToken = new CancellationToken();
            _mockQueue.Setup(p => p.ExistsAsync()).ReturnsAsync(true);
            var exception = new StorageException(
                new RequestResult
                {
                    HttpStatusCode = 503
                },
                string.Empty, new Exception());
            _mockQueue.Setup(p => p.GetMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), null, null, cancellationToken)).Throws(exception);

            for (int i = 0; i < 5; i++)
            {
                var result = await _listener.ExecuteAsync(cancellationToken);
                await result.Wait;
            }

            _mockQueue.Verify(p => p.ExistsAsync(), Times.Exactly(5));
            _mockQueue.Verify(p => p.GetMessagesAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), null, null, cancellationToken), Times.Exactly(5));
        }

        [Fact]
        public async Task ProcessMessageAsync_QueueBeginProcessingMessageReturnsFalse_MessageNotProcessed()
        {
            CancellationToken cancellationToken = new CancellationToken();
            _mockQueueProcessor.Setup(p => p.BeginProcessingMessageAsync(_queueMessage, cancellationToken)).ReturnsAsync(false);

            await _listener.ProcessMessageAsync(_queueMessage, TimeSpan.FromMinutes(10), cancellationToken);
        }

        [Fact]
        public async Task ProcessMessageAsync_FunctionInvocationFails()
        {
            CancellationToken cancellationToken = new CancellationToken();
            FunctionResult result = new FunctionResult(false);
            _mockQueueProcessor.Setup(p => p.BeginProcessingMessageAsync(_queueMessage, cancellationToken)).ReturnsAsync(true);
            _mockTriggerExecutor.Setup(p => p.ExecuteAsync(_queueMessage, cancellationToken)).ReturnsAsync(result);
            _mockQueueProcessor.Setup(p => p.CompleteProcessingMessageAsync(_queueMessage, result, cancellationToken)).Returns(Task.FromResult(true));

            await _listener.ProcessMessageAsync(_queueMessage, TimeSpan.FromMinutes(10), cancellationToken);
        }
        public class TestFixture : IDisposable
        {
            private const string TestQueuePrefix = "queuelistenertests";

            public TestFixture()
            {
                // Create a default host to get some default services
                IHost host = new HostBuilder()
                    .ConfigureDefaultTestHost(b =>
                    {
                        b.AddAzureStorage();
                    })
                    .Build();

                var storageAccount = host.GetStorageAccount();
                QueueClient = storageAccount.CreateCloudQueueClient();

                string queueName = string.Format("{0}-{1}", TestQueuePrefix, Guid.NewGuid());
                Queue = QueueClient.GetQueueReference(queueName);
                Queue.CreateIfNotExistsAsync(null, null, CancellationToken.None).Wait();

                string poisonQueueName = string.Format("{0}-poison", queueName);
                PoisonQueue = QueueClient.GetQueueReference(poisonQueueName);
                PoisonQueue.CreateIfNotExistsAsync(null, null, CancellationToken.None).Wait();
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
                var queue = QueueClient.GetQueueReference(queueName);
                queue.CreateIfNotExistsAsync(null, null, CancellationToken.None).Wait();
                return queue;
            }

            public void Dispose()
            {

                var result = QueueClient.ListQueuesSegmentedAsync(TestQueuePrefix, null).Result;
                var tasks = new List<Task>();

                foreach (var queue in result.Results)
                {
                    tasks.Add(queue.DeleteAsync());
                }

                Task.WaitAll(tasks.ToArray());
            }
        }
    }
}
