// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OData.UriParser;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Executors
{
    public class FunctionExecutorTests
    {
        private FunctionDescriptor _descriptor;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Mock<IFunctionInstance> _mockFunctionInstance;
        private readonly IConfiguration _configuration = new ConfigurationBuilder().Build();

        public FunctionExecutorTests()
        {
            _mockFunctionInstance = new Mock<IFunctionInstance>(MockBehavior.Strict);
            _mockFunctionInstance.Setup(p => p.FunctionDescriptor).Returns(() => _descriptor);

            _cancellationTokenSource = new CancellationTokenSource();
        }

        [Fact]
        public void StartFunctionTimeout_MethodLevelTimeout_CreatesExpectedTimer()
        {
            MethodInfo method = typeof(Functions).GetMethod("MethodLevel", BindingFlags.Static | BindingFlags.Public);
            _descriptor = FunctionIndexer.FromMethod(method, _configuration);

            // we need to set up the Id so that when the timer fires it doesn't throw, but since this is Strict, we need to access it first.
            _mockFunctionInstance.SetupGet(p => p.Id).Returns(Guid.Empty);
            Assert.NotNull(_mockFunctionInstance.Object.Id);

            TimeoutAttribute attribute = method.GetCustomAttribute<TimeoutAttribute>();

            System.Timers.Timer timer = FunctionExecutor.StartFunctionTimeout(_mockFunctionInstance.Object, attribute, _cancellationTokenSource, null);

            Assert.True(timer.Enabled);
            Assert.Equal(attribute.Timeout.TotalMilliseconds, timer.Interval);

            _mockFunctionInstance.VerifyAll();
        }

        [Fact]
        public void StartFunctionTimeout_ClassLevelTimeout_CreatesExpectedTimer()
        {
            MethodInfo method = typeof(Functions).GetMethod("ClassLevel", BindingFlags.Static | BindingFlags.Public);
            _descriptor = FunctionIndexer.FromMethod(method, _configuration);

            // we need to set up the Id so that when the timer fires it doesn't throw, but since this is Strict, we need to access it first.
            _mockFunctionInstance.SetupGet(p => p.Id).Returns(Guid.Empty);
            Assert.NotNull(_mockFunctionInstance.Object.Id);

            TimeoutAttribute attribute = typeof(Functions).GetCustomAttribute<TimeoutAttribute>();

            System.Timers.Timer timer = FunctionExecutor.StartFunctionTimeout(_mockFunctionInstance.Object, attribute, _cancellationTokenSource, null);

            Assert.True(timer.Enabled);
            Assert.Equal(attribute.Timeout.TotalMilliseconds, timer.Interval);

            _mockFunctionInstance.VerifyAll();
        }

        [Fact]
        public void StartFunctionTimeout_NoTimeout_ReturnsNull()
        {
            TimeoutAttribute timeoutAttribute = null;
            System.Timers.Timer timer = FunctionExecutor.StartFunctionTimeout(null, timeoutAttribute, _cancellationTokenSource, null);

            Assert.Null(timer);
        }

        [Fact]
        public void StartFunctionTimeout_NoCancellationTokenParameter_ThrowOnTimeoutFalse_ReturnsNull()
        {
            MethodInfo method = typeof(Functions).GetMethod("NoCancellationTokenParameter", BindingFlags.Static | BindingFlags.Public);
            _descriptor = FunctionIndexer.FromMethod(method, _configuration);

            TimeoutAttribute attribute = typeof(Functions).GetCustomAttribute<TimeoutAttribute>();
            attribute.ThrowOnTimeout = false;

            System.Timers.Timer timer = FunctionExecutor.StartFunctionTimeout(_mockFunctionInstance.Object, attribute, _cancellationTokenSource, null);

            Assert.Null(timer);

            _mockFunctionInstance.VerifyAll();
        }

        [Fact]
        public void StartFunctionTimeout_NoCancellationTokenParameter_ThrowOnTimeoutTrue_CreatesExpectedTimer()
        {
            MethodInfo method = typeof(Functions).GetMethod("NoCancellationTokenParameter", BindingFlags.Static | BindingFlags.Public);
            _descriptor = FunctionIndexer.FromMethod(method, _configuration);

            // we need to set up the Id so that when the timer fires it doesn't throw, but since this is Strict, we need to access it first.
            _mockFunctionInstance.SetupGet(p => p.Id).Returns(Guid.Empty);
            Assert.NotNull(_mockFunctionInstance.Object.Id);

            TimeoutAttribute attribute = typeof(Functions).GetCustomAttribute<TimeoutAttribute>();

            System.Timers.Timer timer = FunctionExecutor.StartFunctionTimeout(_mockFunctionInstance.Object, attribute, _cancellationTokenSource, null);

            Assert.True(timer.Enabled);
            Assert.Equal(attribute.Timeout.TotalMilliseconds, timer.Interval);

            _mockFunctionInstance.VerifyAll();
        }

        [Fact]
        public async Task InvokeAsync_NoCancellation()
        {
            bool called = false;
            Mock<IFunctionInvoker> mockInvoker = new Mock<IFunctionInvoker>();
            mockInvoker.Setup(i => i.InvokeAsync(It.IsAny<object>(), It.IsAny<object[]>()))
                .Returns(() =>
                {
                    called = true;
                    return Task.FromResult<object>(null);
                });

            var timeoutSource = new CancellationTokenSource();
            var shutdownSource = new CancellationTokenSource();
            bool throwOnTimeout = true;

            await FunctionExecutor.InvokeWithTimeoutAsync(mockInvoker.Object, NewArgs(new object[0]), timeoutSource, shutdownSource,
                throwOnTimeout, TimeSpan.MinValue, null);

            Assert.True(called);
        }

        [Fact]
        public async Task InvokeAsync_Timeout_NoThrow()
        {
            bool called = false;
            Mock<IFunctionInvoker> mockInvoker = new Mock<IFunctionInvoker>();
            mockInvoker.Setup(i => i.InvokeAsync(It.IsAny<object>(), It.IsAny<object[]>()))
                .Returns<object, object[]>(async (invokeInstance, invokeParameters) =>
                {
                    var token = (CancellationToken)invokeParameters[0];
                    while (!token.IsCancellationRequested)
                    {
                        await Task.Delay(1000);
                    }
                    called = true;
                    return null;
                });

            var timeoutSource = new CancellationTokenSource();
            var shutdownSource = new CancellationTokenSource();
            object[] parameters = new object[] { shutdownSource.Token };
            bool throwOnTimeout = false;

            timeoutSource.CancelAfter(500);
            await FunctionExecutor.InvokeWithTimeoutAsync(mockInvoker.Object, NewArgs(parameters), timeoutSource, shutdownSource,
                throwOnTimeout, TimeSpan.FromMilliseconds(1), null);

            Assert.True(called);
        }

        [Fact]
        public async Task InvokeAsync_Timeout_Throw()
        {
            Mock<IFunctionInvoker> mockInvoker = new Mock<IFunctionInvoker>();
            mockInvoker.Setup(i => i.InvokeAsync(It.IsAny<object>(), It.IsAny<object[]>()))
                .Returns(async () =>
                {
                    bool exit = false;
                    Task ignore = Task.Delay(5000).ContinueWith((ct) => exit = true);
                    while (!exit)
                    {
                        await Task.Delay(500);
                    }
                    return null;
                });

            // setup the instance details for the exception message
            MethodInfo method = typeof(Functions).GetMethod("ClassLevel", BindingFlags.Static | BindingFlags.Public);
            _descriptor = FunctionIndexer.FromMethod(method, _configuration);
            _mockFunctionInstance.SetupGet(p => p.Id).Returns(Guid.Empty);

            var timeoutSource = new CancellationTokenSource();
            var shutdownSource = new CancellationTokenSource();
            object[] parameters = new object[] { shutdownSource.Token };
            bool throwOnTimeout = true;

            TimeSpan timeoutInterval = TimeSpan.FromMilliseconds(500);
            timeoutSource.CancelAfter(timeoutInterval);
            var ex = await Assert.ThrowsAsync<FunctionTimeoutException>(() => FunctionExecutor.InvokeWithTimeoutAsync(mockInvoker.Object, NewArgs(parameters), timeoutSource, shutdownSource,
                throwOnTimeout, timeoutInterval, _mockFunctionInstance.Object));

            var expectedMessage = string.Format("Timeout value of {0} was exceeded by function: {1}", timeoutInterval, _mockFunctionInstance.Object.FunctionDescriptor.ShortName);
            Assert.Equal(expectedMessage, ex.Message);
        }

        [Fact]
        public async Task InvokeAsync_Stop_NoTimeout()
        {
            bool called = false;
            Mock<IFunctionInvoker> mockInvoker = new Mock<IFunctionInvoker>();
            mockInvoker.Setup(i => i.InvokeAsync(It.IsAny<object>(), It.IsAny<object[]>()))
                .Returns<object, object[]>(async (invokeInstance, invokeParameters) =>
                {
                    var token = (CancellationToken)invokeParameters[0];
                    while (!token.IsCancellationRequested)
                    {
                        await Task.Delay(1000);
                    }
                    called = true;
                    return null;
                });

            var timeoutSource = new CancellationTokenSource();
            var shutdownSource = new CancellationTokenSource();
            object[] parameters = new object[] { shutdownSource.Token };
            bool throwOnTimeout = false;

            shutdownSource.CancelAfter(500);
            await FunctionExecutor.InvokeWithTimeoutAsync(mockInvoker.Object, NewArgs(parameters), timeoutSource, shutdownSource,
                throwOnTimeout, TimeSpan.MinValue, null);

            Assert.True(called);
        }

        [Fact]
        public async Task InvokeAsync_Stop_Timeout_NoThrow()
        {
            bool called = false;
            Mock<IFunctionInvoker> mockInvoker = new Mock<IFunctionInvoker>();
            mockInvoker.Setup(i => i.InvokeAsync(It.IsAny<object>(), It.IsAny<object[]>()))
                .Returns<object, object[]>(async (invokeInstance, invokeParameters) =>
                {
                    var token = (CancellationToken)invokeParameters[0];
                    while (!token.IsCancellationRequested)
                    {
                        await Task.Delay(1500);
                    }
                    called = true;
                    return null;
                });

            var timeoutSource = new CancellationTokenSource();
            var shutdownSource = new CancellationTokenSource();
            object[] parameters = new object[] { shutdownSource.Token };
            bool throwOnTimeout = false;

            shutdownSource.CancelAfter(500);
            timeoutSource.CancelAfter(1000);
            await FunctionExecutor.InvokeWithTimeoutAsync(mockInvoker.Object, NewArgs(parameters), timeoutSource, shutdownSource,
                throwOnTimeout, TimeSpan.MinValue, null);

            Assert.True(called);
        }

        [Fact]
        public async Task InvokeAsync_Stop_Timeout_Throw()
        {
            Mock<IFunctionInvoker> mockInvoker = new Mock<IFunctionInvoker>();
            mockInvoker.Setup(i => i.InvokeAsync(It.IsAny<object>(), It.IsAny<object[]>()))
                .Returns(async () =>
                {
                    bool exit = false;
                    Task ignore = Task.Delay(5000).ContinueWith((ct) => exit = true);
                    while (!exit)
                    {
                        await Task.Delay(500);
                    }
                    return null;
                });

            // setup the instance details for the exception message
            MethodInfo method = typeof(Functions).GetMethod("ClassLevel", BindingFlags.Static | BindingFlags.Public);
            _descriptor = FunctionIndexer.FromMethod(method, _configuration);
            _mockFunctionInstance.SetupGet(p => p.Id).Returns(Guid.Empty);

            var timeoutSource = new CancellationTokenSource();
            var shutdownSource = new CancellationTokenSource();
            object[] parameters = new object[] { shutdownSource.Token };
            bool throwOnTimeout = true;

            TimeSpan timeoutInterval = TimeSpan.FromMilliseconds(1000);
            shutdownSource.CancelAfter(500);
            timeoutSource.CancelAfter(timeoutInterval);
            var ex = await Assert.ThrowsAsync<FunctionTimeoutException>(() => FunctionExecutor.InvokeWithTimeoutAsync(mockInvoker.Object, NewArgs(parameters), timeoutSource, shutdownSource,
                 throwOnTimeout, timeoutInterval, _mockFunctionInstance.Object));

            var expectedMessage = string.Format("Timeout value of {0} was exceeded by function: {1}", timeoutInterval, _mockFunctionInstance.Object.FunctionDescriptor.ShortName);
            Assert.Equal(expectedMessage, ex.Message);
        }

        private FunctionExecutor GetTestFunctionExecutor(DrainModeManager drainModeManager = null)
        {
            var mockFunctionInstanceLogger = new Mock<IFunctionInstanceLogger>();
            var mockFunctionOutputLogger = new NullFunctionOutputLogger();
            var mockExceptionHandler = new Mock<IWebJobsExceptionHandler>();
            var mockFunctionEventCollector = new Mock<IAsyncCollector<FunctionInstanceLogEntry>>();
            var mockConcurrencyManager = new Mock<ConcurrencyManager>();

            var functionExecutor = new FunctionExecutor(
                mockFunctionInstanceLogger.Object,
                mockFunctionOutputLogger,
                mockExceptionHandler.Object,
                mockFunctionEventCollector.Object,
                mockConcurrencyManager.Object,
                NullLoggerFactory.Instance,
                null,
                drainModeManager);

            return functionExecutor;
        }

        private static void TestFunction()
        {
            // used for a FunctionDescriptor
        }

        private IFunctionInstanceEx GetFunctionInstanceExMockInstance()
        {
            var mockBindingSource = new Mock<IBindingSource>();
            var valueProviders = Task.Run(() =>
            {
                IDictionary<string, IValueProvider> d = new Dictionary<string, IValueProvider>();
                IReadOnlyDictionary<string, IValueProvider> red = new ReadOnlyDictionary<string, IValueProvider>(d);
                return red;
            });
            mockBindingSource.Setup(p => p.BindAsync(It.IsAny<ValueBindingContext>())).Returns(valueProviders);

            MethodInfo method = typeof(Functions).GetMethod("MethodLevel", BindingFlags.Static | BindingFlags.Public);
            TimeoutAttribute attribute = method.GetCustomAttribute<TimeoutAttribute>();
            FunctionDescriptor descriptor = new FunctionDescriptor();
            descriptor.TimeoutAttribute = attribute;
            descriptor.ClassLevelFilters = new List<IFunctionFilter>();
            descriptor.MethodLevelFilters = new List<IFunctionFilter>();

            var mockfunctionInstance = new Mock<IFunctionInstanceEx>();
            mockfunctionInstance.Setup(p => p.BindingSource).Returns(mockBindingSource.Object);
            mockfunctionInstance.Setup(p => p.Invoker.ParameterNames).Returns(new List<string>());
            mockfunctionInstance.Setup(p => p.FunctionDescriptor).Returns(descriptor);
            return mockfunctionInstance.Object;
        }

        [Fact]
        public async Task ExecuteLoggingAsync_WithDrainModeManagerNull_SkipsDrainModeOperations()
        {
            var mockFunctionInstanceEx = GetFunctionInstanceExMockInstance();
            var parameterHelper = new FunctionExecutor.ParameterHelper(mockFunctionInstanceEx);
            var logger = new TestLogger("Tests.FunctionExecutor");
            var functionExecutor = GetTestFunctionExecutor();
            try
            {
                await functionExecutor.ExecuteWithLoggingAsync(mockFunctionInstanceEx, new FunctionStartedMessage(), new FunctionInstanceLogEntry(), parameterHelper, logger, CancellationToken.None);
            }
            // Function Invocation Exception is expected
            catch (FunctionInvocationException)
            {
            }
        }

        [Fact]
        public void OnFunctionTimeout_PerformsExpectedOperations()
        {
            RunOnFunctionTimeoutTest(false, "Initiating cancellation.");
        }

        [Fact]
        public void OnFunctionTimeout_DoesNotCancel_IfDebugging()
        {
            RunOnFunctionTimeoutTest(true, "Function will not be cancelled while debugging.");
        }

        [Fact]
        public async Task GetStatus_Returns_Expected()
        {
            var triggerData = new TriggeredFunctionData
            {
                TriggerValue = 123,
                TriggerDetails = new Dictionary<string, string>()
            };
            var functionDescriptor = FunctionExecutorTestHelper.GetFunctionDescriptor();
            var functionInstance = FunctionExecutorTestHelper.CreateFunctionInstance(Guid.NewGuid(), triggerData.TriggerDetails, false, functionDescriptor, 1000);
            FunctionExecutor executor = GetTestFunctionExecutor();

            // Arrange
            HostStartedMessage testMessage = new HostStartedMessage();
            executor.HostOutputMessage = testMessage;

            FunctionActivityStatus status = executor.GetStatus();

            Assert.Equal(status.OutstandingInvocations, 0);
            Assert.Equal(status.OutstandingRetries, 0);

            ManualResetEvent resetEvent = new ManualResetEvent(false);
            Task monitoringTask = Task.Run(async () =>
            {
                // validate that we have 2 active invocation and 2 retries
                await TestHelpers.Await(() =>
                {
                    var status = (executor as IFunctionActivityStatusProvider).GetStatus();
                    return status.OutstandingInvocations == 2 && status.OutstandingRetries == 2;
                }, 5000);
            });

            Task executionTask1 = Task.Run(async () =>
            {
                (executor as IRetryNotifier).RetryPending();
                await executor.TryExecuteAsync(functionInstance, CancellationToken.None);
                (executor as IRetryNotifier).RetryComplete();
            });

            Task executionTask2 = Task.Run(async () =>
            {
                (executor as IRetryNotifier).RetryPending();
                await executor.TryExecuteAsync(functionInstance, CancellationToken.None);
                (executor as IRetryNotifier).RetryComplete();
            });

            await Task.WhenAll(monitoringTask, executionTask1, executionTask2);

            // validate that there are no active invocations or retries in 
            status = executor.GetStatus();
            Assert.Equal(status.OutstandingInvocations, 0);
            Assert.Equal(status.OutstandingRetries, 0);
        }

        private void RunOnFunctionTimeoutTest(bool isDebugging, string expectedMessage)
        {
            System.Timers.Timer timer = new System.Timers.Timer(TimeSpan.FromMinutes(1).TotalMilliseconds);
            timer.Start();

            Assert.True(timer.Enabled);
            Assert.False(_cancellationTokenSource.IsCancellationRequested);

            MethodInfo method = typeof(Functions).GetMethod("MethodLevel", BindingFlags.Static | BindingFlags.Public);
            TimeoutAttribute attribute = method.GetCustomAttribute<TimeoutAttribute>();
            _descriptor = FunctionIndexer.FromMethod(method, _configuration);
            Guid instanceId = Guid.Parse("B2D1DD72-80E2-412B-A22E-3B4558F378B4");
            bool timeoutWhileDebugging = false;

            TestLogger logger = new TestLogger("Tests.FunctionExecutor");

            FunctionExecutor.OnFunctionTimeout(timer, _descriptor, instanceId, attribute.Timeout, timeoutWhileDebugging, logger, _cancellationTokenSource, () => isDebugging);

            Assert.False(timer.Enabled);
            Assert.NotEqual(isDebugging, _cancellationTokenSource.IsCancellationRequested);

            string message = string.Format("Timeout value of 00:01:00 exceeded by function 'Functions.MethodLevel' (Id: 'b2d1dd72-80e2-412b-a22e-3b4558f378b4'). {0}", expectedMessage);

            // verify ILogger
            LogMessage log = logger.GetLogMessages().Single();
            Assert.Equal(LogLevel.Error, log.Level);
            Assert.Equal(message, log.FormattedMessage);
        }

        private static FunctionExecutor.ParameterHelper NewArgs(object[] args)
        {
            var parameters = new FunctionExecutor.ParameterHelper();
            parameters.InvokeParameters = args;
            return parameters;
        }

        public static void GlobalLevel(CancellationToken cancellationToken)
        {
        }

        [Timeout("00:02:00", ThrowOnTimeout = true, TimeoutWhileDebugging = true)]
        public static class Functions
        {
            [Timeout("00:01:00", ThrowOnTimeout = true, TimeoutWhileDebugging = true)]
            public static void MethodLevel(CancellationToken cancellationToken)
            {
            }

            public static void ClassLevel(CancellationToken cancellationToken)
            {
            }

            public static void NoCancellationTokenParameter()
            {
            }
        }
    }
}