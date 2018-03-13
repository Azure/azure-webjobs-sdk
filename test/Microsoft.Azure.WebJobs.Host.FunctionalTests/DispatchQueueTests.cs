// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Dispatch;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.FunctionalTests.TestDoubles;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Host.Queues.Listeners;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests
{
    public class DispatchQueueTests
    {
        private const string HostId = "functionalTestHost";

        // provide default mock
        private Mock<IHostIdProvider> _hostIdMock;
        private Mock<IWebJobsExceptionHandler> _exceptionMock;
        private Mock<IContextSetter<IMessageEnqueuedWatcher>> _messageEnqueueSetterMock;
        private Mock<IStorageAccountProvider> _accountProviderMock;

        private IQueueConfiguration _queueConfiguration;
        private ISharedContextProvider _sharedContextProvider;
        private SharedQueueHandler _sharedQueue;

        private TestLoggerProvider _loggerProvider = new TestLoggerProvider();

        public DispatchQueueTests()
        {
            _accountProviderMock = new Mock<IStorageAccountProvider>();
            IStorageAccountProvider accountProvider = new FakeStorageAccountProvider
            {
                StorageAccount = new FakeStorageAccount()
            };
            _accountProviderMock.Setup(m => m.TryGetAccountAsync(ConnectionStringNames.Storage, It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((name, cancel) => accountProvider.TryGetAccountAsync(name, cancel));

            _hostIdMock = new Mock<IHostIdProvider>();
            _hostIdMock.Setup(m => m.GetHostIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(HostId);

            _exceptionMock = new Mock<IWebJobsExceptionHandler>();

            _queueConfiguration = new FakeQueueConfiguration(accountProvider);

            _sharedContextProvider = new SharedContextProvider();

            _messageEnqueueSetterMock = new Mock<IContextSetter<IMessageEnqueuedWatcher>>();

            ILoggerFactory factory = new LoggerFactory();
            factory.AddProvider(_loggerProvider);

            _sharedQueue = new SharedQueueHandler(_accountProviderMock.Object,
                                                _hostIdMock.Object,
                                                _exceptionMock.Object,
                                                factory,
                                                _queueConfiguration,
                                                _sharedContextProvider,
                                                _messageEnqueueSetterMock.Object
                                                );
        }

        [Fact]
        public async Task InMemoryDispatchQueueHandlerTest()
        {
            string error = "no storage account found";
            _accountProviderMock.Setup(m => m.TryGetAccountAsync(ConnectionStringNames.Storage, It.IsAny<CancellationToken>())).
                ThrowsAsync(new Exception(error));

            await _sharedQueue.InitializeAsync(CancellationToken.None);
            Assert.Empty(_loggerProvider.GetAllLogMessages());

            // listenercontext should return inMemoryDispatchQueueHandler when there's no storage account
            var descriptorMock = new Mock<FunctionDescriptor>();
            var triggerExecutorMock = new Mock<ITriggeredFunctionExecutor>();
            ListenerFactoryContext context = new ListenerFactoryContext(
                descriptorMock.Object,
                triggerExecutorMock.Object,
                _sharedQueue,
                CancellationToken.None);

            var messageHandlerMock = new Mock<IMessageHandler>();
            var condition = new AutoResetEvent(false);
            messageHandlerMock.Setup(m => m.TryExecuteAsync(It.IsAny<JObject>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    await Task.Yield();
                    condition.Set();  // will run asynchronously
                    return new FunctionResult(true);
                });

            var dispatchQueue = context.GetDispatchQueue(messageHandlerMock.Object);
            // make sure initialization error is traced
            Assert.Equal(error, _loggerProvider.GetAllLogMessages().Single().Exception.Message);
            Assert.Equal(SharedQueueHandler.InitErrorMessage, _loggerProvider.GetAllLogMessages().Single().FormattedMessage);
            Assert.IsType<InMemoryDispatchQueueHandler>(dispatchQueue);

            await dispatchQueue.EnqueueAsync(JObject.Parse("{}"), CancellationToken.None);
            // without storage account, it is still possible to perform local enqueue, dequeue
            Assert.True(condition.WaitOne(200));

            // following two should be NOOP, inner queueListener was never created
            await _sharedQueue.StartQueueAsync(CancellationToken.None);
            await _sharedQueue.StopQueueAsync(CancellationToken.None);
            // no NullPointerException
        }

        [Fact]
        public async Task QueueInitializationTest()
        {
            // first initialization should be fine
            await _sharedQueue.InitializeAsync(CancellationToken.None);

            // should not initialize twice
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                            () => _sharedQueue.InitializeAsync(CancellationToken.None));
            Assert.Equal($"Expected state to be \"Created\" but actualy state is \"Initialized\", this is probably because methods are not called in correct order",
                            exception.Message);
        }

        [Fact]
        public async Task HotPathNotificationTest()
        {
            await _sharedQueue.InitializeAsync(CancellationToken.None);

            var messageHandlerMock = new Mock<IMessageHandler>();
            var calls = 0;
            messageHandlerMock.Setup(m => m.TryExecuteAsync(It.IsAny<JObject>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    calls++; // executed sequentially, no need interlocked
                    return new FunctionResult(true);
                });

            Assert.True(_sharedQueue.RegisterHandler("mockFunction1", messageHandlerMock.Object));

            // register another INotificationHandler to sharedQueueWatcher
            // so that we can tell when the sharedQueueListener is notified
            var notifies = 0;
            var notificationMock = new Mock<INotificationCommand>();
            notificationMock.Setup(m => m.Notify()).Callback(() => Interlocked.Increment(ref notifies));
            _sharedContextProvider.GetOrCreateInstance<SharedQueueWatcher>(null)
                .Register(HostQueueNames.GetHostSharedQueueName(HostId), notificationMock.Object);

            await _sharedQueue.StartQueueAsync(CancellationToken.None);

            int max = 10;
            var enqueue = new List<Task>();
            for (int i = 0; i < max; i++)
            {
                JObject message = JObject.Parse("{count:" + i + "}");
                enqueue.Add(_sharedQueue.EnqueueAsync(message, "mockFunction1", CancellationToken.None));
            }

            await Task.WhenAll(enqueue);
            // wait for dequeue
            await TestHelpers.Await(() => calls >= max, 1000, 200);
            await _sharedQueue.StopQueueAsync(CancellationToken.None);

            Assert.Equal(max, notifies);
            Assert.Equal(max, calls);
        }

        [Fact]
        public async Task DequeueBehaviorTests()
        {
            await _sharedQueue.InitializeAsync(CancellationToken.None);

            var testCases = new List<TestCase>();
            // have three functions
            // A run X times  => expect X times
            TestCase a = new TestCase
            {
                CallCount = 0,
                TotalEnqueues = 3,
                Register = true
            };
            testCases.Add(a);
            // B run Y times  => expect Y times
            TestCase b = new TestCase
            {
                CallCount = 0,
                TotalEnqueues = 5,
                Register = true
            };
            testCases.Add(b);
            // C run Z times  => expect 0 times
            TestCase c = new TestCase
            {
                CallCount = 0,
                TotalEnqueues = 7,
                Register = false //(disabled)
            };
            testCases.Add(c);

            // start enqueuing
            await RunDummyEnqueueAsync(testCases);
            // start dequeuing
            await _sharedQueue.StartQueueAsync(CancellationToken.None);

            // wait for dequeue
            await TestHelpers.Await(() => a.CallCount >= a.TotalEnqueues && b.CallCount >= b.TotalEnqueues, 1000, 200);
            await _sharedQueue.StopQueueAsync(CancellationToken.None);

            Assert.Equal(a.TotalEnqueues, a.CallCount);
            Assert.Equal(b.TotalEnqueues, b.CallCount);
            Assert.Equal(0, c.CallCount);
        }

        private class TestCase
        {
            public int CallCount { get; set; }
            public int TotalEnqueues { get; set; }
            public bool Register { get; set; }
        }

        private Task RunDummyEnqueueAsync(List<TestCase> testCases)
        {
            var enqueues = new List<Task>();
            int index = 0;
            foreach (var testcase in testCases)
            {
                var functionMock = new Mock<IMessageHandler>();
                functionMock.Setup(m => m.TryExecuteAsync(It.IsAny<JObject>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => { testcase.CallCount++; return new FunctionResult(true); });
                if (testcase.Register)
                {
                    Assert.True(_sharedQueue.RegisterHandler(index.ToString(), functionMock.Object));
                }
                for (int i = 0; i < testcase.TotalEnqueues; i++)
                {
                    // enqueue an empty JObject
                    enqueues.Add(_sharedQueue.EnqueueAsync(JObject.Parse("{}"), index.ToString(), CancellationToken.None));
                }
                index++;
            }
            return Task.WhenAll(enqueues);
        }
    }
}
