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
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Executors
{
    public class TriggeredFunctionExecutorTests
    {
        private ConcurrencyManager _concurrencyManager;

        [Fact]
        public async Task TryExecuteAsync_WithInvokeHandler_InvokesHandler()
        {
            var mockExecutor = new Mock<IFunctionExecutor>();
            mockExecutor.Setup(m => m.TryExecuteAsync(It.IsAny<IFunctionInstance>(), It.IsAny<CancellationToken>())).
                Returns<IFunctionInstance, CancellationToken>((x, y) =>
                {
                    x.Invoker.InvokeAsync(null, null).Wait();
                    return Task.FromResult<IDelayedException>(null);
                });

            bool innerInvokerInvoked = false;
            Mock<IFunctionInvokerEx> mockInvoker = new Mock<IFunctionInvokerEx>();
            mockInvoker.Setup(m => m.InvokeAsync(null, null)).Returns(() =>
            {
                innerInvokerInvoked = true;
                return Task.FromResult<object>(null);
            });

            bool customInvokerInvoked = false;
            Func<Func<Task>, Task> invokeHandler = async (inner) =>
            {
                customInvokerInvoked = true;
                await inner();
            };

            var mockTriggerBinding = new Mock<ITriggeredFunctionBinding<int>>();
            var functionDescriptor = new FunctionDescriptor();
            var serviceScopeFactoryMock = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
            var instanceFactory = new TriggeredFunctionInstanceFactory<int>(mockTriggerBinding.Object, mockInvoker.Object, functionDescriptor, serviceScopeFactoryMock.Object);
            var triggerExecutor = new TriggeredFunctionExecutor<int>(functionDescriptor, mockExecutor.Object, instanceFactory, NullLoggerFactory.Instance);

            // specify a custom handler on the trigger data and
            // verify it is invoked when the trigger executes
            var triggerData = new TriggeredFunctionData
            {
                TriggerValue = 123,
                InvokeHandler = invokeHandler
            };

            var result = await triggerExecutor.TryExecuteAsync(triggerData, CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.True(customInvokerInvoked);
            Assert.True(innerInvokerInvoked);
        }

        [Theory]
        [InlineData(true, null)]
        [InlineData(true, "TestSharedListener")]
        [InlineData(false, null)]
        public async Task TryExecuteAsync_DynamicConcurrencyEnabled_TracksFunctionInvocations(bool dynamicConcurrencyEnabled, string sharedListenerFunctionId)
        {
            var triggerData = new TriggeredFunctionData
            {
                TriggerValue = 123,
                TriggerDetails = new Dictionary<string, string>()
            };
            var functionDescriptor = FunctionExecutorTestHelper.GetFunctionDescriptor();
            var functionInstance = FunctionExecutorTestHelper.CreateFunctionInstance(Guid.NewGuid(), triggerData.TriggerDetails, false, functionDescriptor);
            string functionId = functionInstance.FunctionDescriptor.Id;
            if (sharedListenerFunctionId != null)
            {
                functionDescriptor.SharedListenerId = sharedListenerFunctionId;
                functionId = sharedListenerFunctionId;
            }
            var instaceFactoryMock = new Mock<ITriggeredFunctionInstanceFactory<int>>();
            instaceFactoryMock.Setup(m => m.Create(It.IsAny<FunctionInstanceFactoryContext<int>>())).Returns(functionInstance);

            var concurrencyOptions = new ConcurrencyOptions
            {
                DynamicConcurrencyEnabled = dynamicConcurrencyEnabled
            };
            var functionExecutor = GetTestFunctionExecutor(concurrencyOptions: concurrencyOptions);
            var triggerExecutor = new TriggeredFunctionExecutor<int>(functionDescriptor, functionExecutor, instaceFactoryMock.Object, NullLoggerFactory.Instance);

            ConcurrencyStatus concurrencyStatus = null;
            if (dynamicConcurrencyEnabled)
            {
                // need to simulate the listener beginning the invocation
                concurrencyStatus = _concurrencyManager.GetStatus(functionId);
                Assert.Single(_concurrencyManager.ConcurrencyStatuses);
                Assert.Equal(1, concurrencyStatus.GetAvailableInvocationCount(0));
                Assert.Equal(0, concurrencyStatus.OutstandingInvocations);
                Assert.Equal(0, concurrencyStatus.InvocationsSinceLastAdjustment);
                Assert.Equal(0, concurrencyStatus.MaxConcurrentExecutionsSinceLastAdjustment);
            }

            var result = await triggerExecutor.TryExecuteAsync(triggerData, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Null(result.Exception);

            if (dynamicConcurrencyEnabled)
            {
                // verify concurrency manager tracked the invocation
                concurrencyStatus = _concurrencyManager.ConcurrencyStatuses[functionId];
                Assert.Equal(1, concurrencyStatus.GetAvailableInvocationCount(0));
                Assert.Equal(0, concurrencyStatus.OutstandingInvocations);
                Assert.Equal(1, concurrencyStatus.InvocationsSinceLastAdjustment);
                Assert.Equal(1, concurrencyStatus.MaxConcurrentExecutionsSinceLastAdjustment);
            } 
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task TryExecuteWithRetries_Test(bool invocationThrows)
        {
            var triggerData = new TriggeredFunctionData
            {
                TriggerValue = 123,
                TriggerDetails = new Dictionary<string, string>()
            };
            var functionDescriptor = FunctionExecutorTestHelper.GetFunctionDescriptor();
            var functionInstance = FunctionExecutorTestHelper.CreateFunctionInstance(Guid.NewGuid(), triggerData.TriggerDetails, invocationThrows, functionDescriptor);
            var serviceScopeFactoryMock = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
            var instaceFactoryMock = new Mock<ITriggeredFunctionInstanceFactory<int>>();

            var functionExecutor = GetTestFunctionExecutor();

            instaceFactoryMock.Setup(m => m.Create(It.IsAny<FunctionInstanceFactoryContext<int>>())).Returns(functionInstance);

            var testLogger = new TestLogger("Test");
            Mock<ILoggerFactory> factoryMock = new Mock<ILoggerFactory>(MockBehavior.Strict);
            factoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(testLogger);
            var triggerExecutor = new TriggeredFunctionExecutor<int>(functionDescriptor, functionExecutor, instaceFactoryMock.Object, factoryMock.Object);

            // Arrange
            HostStartedMessage testMessage = new HostStartedMessage();
            functionExecutor.HostOutputMessage = testMessage;

            int maxRetryCount = 5;
            TimeSpan delay = TimeSpan.FromMilliseconds(100);
            var mockRetryStrategy = new Mock<IRetryStrategy>();
            mockRetryStrategy.Setup(p => p.MaxRetryCount).Returns(maxRetryCount);
            mockRetryStrategy.Setup(p => p.GetNextDelay(It.IsAny<RetryContext>())).Returns(delay);

            functionDescriptor.RetryStrategy = mockRetryStrategy.Object;

            var result = await triggerExecutor.TryExecuteAsync(triggerData, CancellationToken.None);

            if (invocationThrows)
            {
                var messages = testLogger.GetLogMessages().Select(p => p.FormattedMessage).ToArray();
                Assert.Single(messages.Where(x => x == "Function execution failed after '5' retries."));
                Assert.Equal(5, messages.Where(x => x.StartsWith("Waiting for `")).Count());
                Assert.NotNull(result.Exception.InnerException);
                Assert.Equal("Test retry exception. invocationCount:6", result.Exception.InnerException.Message);
            }
            else
            {
                Assert.NotNull(result);
                Assert.Null(result.Exception);
            }
        }

        private FunctionExecutor GetTestFunctionExecutor(DrainModeManager drainModeManager = null, ConcurrencyOptions concurrencyOptions = null)
        {
            var mockFunctionInstanceLogger = new Mock<IFunctionInstanceLogger>();
            var mockFunctionOutputLogger = new NullFunctionOutputLogger();
            var mockExceptionHandler = new Mock<IWebJobsExceptionHandler>();
            var mockFunctionEventCollector = new Mock<IAsyncCollector<FunctionInstanceLogEntry>>();

            concurrencyOptions = concurrencyOptions ?? new ConcurrencyOptions();
            var optionsWrapper = new OptionsWrapper<ConcurrencyOptions>(concurrencyOptions);
            var mockConcurrencyThrottleManager = new Mock<IConcurrencyThrottleManager>(MockBehavior.Strict);
            _concurrencyManager = new ConcurrencyManager(optionsWrapper, NullLoggerFactory.Instance, mockConcurrencyThrottleManager.Object);

            var functionExecutor = new FunctionExecutor(
                mockFunctionInstanceLogger.Object,
                mockFunctionOutputLogger,
                mockExceptionHandler.Object,
                mockFunctionEventCollector.Object,
                _concurrencyManager,
                NullLoggerFactory.Instance,
                null,
                drainModeManager);

            functionExecutor.HostOutputMessage = new HostStartedMessage();

            return functionExecutor;
        }
    }
}