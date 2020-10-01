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
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Executors
{
    public class TriggeredFunctionExecutorTests
    {
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
        [InlineData(false)]
        [InlineData(true)]
        public async Task TryExecuteWithRetries_Test(bool invocationThrows)
        {
            var functionInstance = (IFunctionInstance)CreateFunctionInstance(Guid.NewGuid(), invocationThrows);
            var logger = new TestLogger("Tests.FunctionExecutor");
            Mock<IFunctionInvokerEx> mockInvoker = new Mock<IFunctionInvokerEx>();
            var mockTriggerBinding = new Mock<ITriggeredFunctionBinding<int>>();
            var functionDescriptor = new FunctionDescriptor();
            var serviceScopeFactoryMock = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
            var instaceFactoryMock = new Mock<ITriggeredFunctionInstanceFactory<int>>();

            var functionExecutor = GetTestFunctionExecutor();

            instaceFactoryMock.Setup(m => m.Create(It.IsAny<FunctionInstanceFactoryContext<int>>())).Returns(functionInstance);

            var triggerExecutor = new TriggeredFunctionExecutor<int>(functionDescriptor, functionExecutor, instaceFactoryMock.Object, NullLoggerFactory.Instance);
            
            // Arrange
            HostStartedMessage testMessage = new HostStartedMessage();
            functionExecutor.HostOutputMessage = testMessage;

            int maxRetryCount = 5;
            TimeSpan delay = TimeSpan.FromMilliseconds(100);
            var mockRetryStrategy = new Mock<IRetryStrategy>();
            mockRetryStrategy.Setup(p => p.MaxRetryCount).Returns(maxRetryCount);
            mockRetryStrategy.Setup(p => p.GetNextDelay(It.IsAny<RetryContext>())).Returns(delay);

            functionDescriptor.RetryStrategy = mockRetryStrategy.Object;

            var triggerData = new TriggeredFunctionData
            {
                TriggerValue = 123
            };

            var result = await triggerExecutor.TryExecuteAsync(triggerData, CancellationToken.None);

            if (invocationThrows)
            {
                Assert.NotNull(result);
                Assert.NotNull(result.Exception.InnerException);
                Assert.Equal("Test retry exception. invocationCount:6", result.Exception.InnerException.Message);
            }
            else
            {
                Assert.NotNull(result);
                Assert.Null(result.Exception);
            }
        }

        private FunctionDescriptor GetFunctionDescriptor()
        {
            var method = GetType().GetMethod(nameof(TestFunction), BindingFlags.NonPublic | BindingFlags.Static);
            return FunctionIndexer.FromMethod(method, new ConfigurationBuilder().Build());
        }

        private IFunctionInstance CreateFunctionInstance(Guid id, bool invocationThrows)
        {
            var descriptor = GetFunctionDescriptor();
            var serviceScopeFactoryMock = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
            var serviceScopeMock = new Mock<IServiceScope>();
            serviceScopeFactoryMock.Setup(s => s.CreateScope()).Returns(serviceScopeMock.Object);
            Mock<IFunctionInvoker> mockInvoker = new Mock<IFunctionInvoker>();
            int invocationCount = 0;
            mockInvoker.Setup(i => i.InvokeAsync(It.IsAny<object>(), It.IsAny<object[]>()))
                .Returns(() =>
                {
                    invocationCount++;
                    if (invocationThrows)
                    {
                        throw new Exception($"Test retry exception. invocationCount:{invocationCount}");
                    }
                    return Task.FromResult<object>(null);
                });
            mockInvoker.Setup(m => m.ParameterNames).Returns(new List<string>());
            var mockBindingSource = new Mock<IBindingSource>();
            var valueProviders = Task.Run(() =>
            {
                IDictionary<string, IValueProvider> d = new Dictionary<string, IValueProvider>();
                IReadOnlyDictionary<string, IValueProvider> red = new ReadOnlyDictionary<string, IValueProvider>(d);
                return red;
            });
            mockBindingSource.Setup(p => p.BindAsync(It.IsAny<ValueBindingContext>())).Returns(valueProviders);
            return new FunctionInstance(id, new Dictionary<string, string>(), null, new ExecutionReason(), mockBindingSource.Object, mockInvoker.Object, descriptor, serviceScopeFactoryMock.Object);
        }

        private FunctionExecutor GetTestFunctionExecutor(DrainModeManager drainModeManager = null)
        {
            var mockFunctionInstanceLogger = new Mock<IFunctionInstanceLogger>();
            var mockFunctionOutputLogger = new NullFunctionOutputLogger();
            var mockExceptionHandler = new Mock<IWebJobsExceptionHandler>();
            var mockFunctionEventCollector = new Mock<IAsyncCollector<FunctionInstanceLogEntry>>();

            var functionExecutor = new FunctionExecutor(
                mockFunctionInstanceLogger.Object,
                mockFunctionOutputLogger,
                mockExceptionHandler.Object,
                mockFunctionEventCollector.Object,
                NullLoggerFactory.Instance,
                null,
                drainModeManager);

            return functionExecutor;
        }

        [FixedDelayRetry(5, "00:00:01")]
        private static void TestFunction()
        {
            // used for a FunctionDescriptor
        }
    }
}