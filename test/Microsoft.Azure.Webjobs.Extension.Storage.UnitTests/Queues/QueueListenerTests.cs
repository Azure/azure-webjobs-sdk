﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Host.Queues.Listeners;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Queues
{
    public class QueueListenerTests
    {
        private Mock<CloudQueue> _mockQueue;
        private QueueListener _listener;
        private Mock<QueueProcessor> _mockQueueProcessor;
        private Mock<ITriggerExecutor<CloudQueueMessage>> _mockTriggerExecutor;
        private CloudQueueMessage _storageMessage;
        private ILoggerFactory _loggerFactory;

        public QueueListenerTests()
        {
            // CloudQueue queue = new CloudQueue(new Uri("https://test.queue.core.windows.net/testqueue"));
            //_mockQueue = new Mock<CloudQueue>(MockBehavior.Strict);
            _mockQueue = new Mock<CloudQueue>(new Uri("https://test.queue.core.windows.net/testqueue"));
            // _mockQueue.Setup(p => p.SdkObject).Returns(queue);

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

            CloudQueueMessage cloudMessage = new CloudQueueMessage("TestMessage");
            _storageMessage = cloudMessage;
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
            _mockQueueProcessor.Setup(p => p.BeginProcessingMessageAsync(_storageMessage, cancellationToken)).ReturnsAsync(true);
            _mockTriggerExecutor.Setup(p => p.ExecuteAsync(_storageMessage, cancellationToken)).ReturnsAsync(result);
            _mockQueueProcessor.Setup(p => p.CompleteProcessingMessageAsync(_storageMessage, result, cancellationToken)).Returns(Task.FromResult(true));

            await _listener.ProcessMessageAsync(_storageMessage, TimeSpan.FromMinutes(10), cancellationToken);
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
        public async Task ProcessMessageAsync_QueueBeginProcessingMessageReturnsFalse_MessageNotProcessed()
        {
            CancellationToken cancellationToken = new CancellationToken();
            _mockQueueProcessor.Setup(p => p.BeginProcessingMessageAsync(_storageMessage, cancellationToken)).ReturnsAsync(false);

            await _listener.ProcessMessageAsync(_storageMessage, TimeSpan.FromMinutes(10), cancellationToken);
        }

        [Fact]
        public async Task ProcessMessageAsync_FunctionInvocationFails()
        {
            CancellationToken cancellationToken = new CancellationToken();
            FunctionResult result = new FunctionResult(false);
            _mockQueueProcessor.Setup(p => p.BeginProcessingMessageAsync(_storageMessage, cancellationToken)).ReturnsAsync(true);
            _mockTriggerExecutor.Setup(p => p.ExecuteAsync(_storageMessage, cancellationToken)).ReturnsAsync(result);
            _mockQueueProcessor.Setup(p => p.CompleteProcessingMessageAsync(_storageMessage, result, cancellationToken)).Returns(Task.FromResult(true));

            await _listener.ProcessMessageAsync(_storageMessage, TimeSpan.FromMinutes(10), cancellationToken);
        }
    }
}
