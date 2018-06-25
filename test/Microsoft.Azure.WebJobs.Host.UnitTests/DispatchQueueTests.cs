// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Dispatch;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Queue.Protocol;
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

        private ISharedContextProvider _sharedContextProvider;
        private SharedQueueHandler _sharedQueue;

        private TestLoggerProvider _loggerProvider = new TestLoggerProvider();

        public DispatchQueueTests()
        {

            _hostIdMock = new Mock<IHostIdProvider>();
            _hostIdMock.Setup(m => m.GetHostIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(HostId);

            _exceptionMock = new Mock<IWebJobsExceptionHandler>();

            _sharedContextProvider = new SharedContextProvider();

            ILoggerFactory factory = new LoggerFactory();
            factory.AddProvider(_loggerProvider);

            ILoadBalancerQueue loadBalancerQueue = new InMemoryLoadBalancerQueue();

            _sharedQueue = new SharedQueueHandler(_hostIdMock.Object, _exceptionMock.Object,
                factory, _sharedContextProvider, loadBalancerQueue);
        }

        [Fact]
        public async Task DelaysInitializationException()
        {
            var sharedQueueName = HostQueueNames.GetHostSharedQueueName(HostId);
            var sharedPoisonQueueName = HostQueueNames.GetHostSharedPoisonQueueName(HostId);

            var mockLoadBalancerQueue = new Mock<ILoadBalancerQueue>();
            mockLoadBalancerQueue
                .Setup(m => m.CreateQueueListener(sharedQueueName, sharedPoisonQueueName, It.IsAny<Func<string, CancellationToken, Task<FunctionResult>>>()))
                .Throws(new InvalidOperationException("boom!"));

            _sharedQueue = new SharedQueueHandler(_hostIdMock.Object, _exceptionMock.Object,
                new LoggerFactory(), _sharedContextProvider, mockLoadBalancerQueue.Object);

            // This should not throw.
            await _sharedQueue.InitializeAsync(CancellationToken.None);

            // This should throw, only when someone tries to use the shared queue.
            var ex = Assert.Throws<InvalidOperationException>(() => _sharedQueue.RegisterHandler("TestFunction", null));
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

            _sharedQueue.RegisterHandler("mockFunction1", messageHandlerMock.Object);

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
                    _sharedQueue.RegisterHandler(index.ToString(), functionMock.Object);
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
