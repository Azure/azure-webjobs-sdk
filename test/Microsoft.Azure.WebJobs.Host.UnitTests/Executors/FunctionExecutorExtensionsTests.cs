// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Executors
{
    public class FunctionExecutorExtensionsTests
    {
        [Fact]
        public async Task TryExecuteWithRetries_ExitRetryLoop()
        {
            TestLoggerProvider loggerProvider = new TestLoggerProvider();
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(loggerProvider);

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                cancellationTokenSource.Cancel();
            });

            Mock<IFunctionInstance> mockFunctionInstance = new Mock<IFunctionInstance>(MockBehavior.Strict);
            FunctionDescriptor functionDescriptor = GetFunctionDescriptor(100, 5);
            mockFunctionInstance.Setup(x => x.FunctionDescriptor).Returns(functionDescriptor);

            Func<IFunctionInstance> instanceFactory = () => mockFunctionInstance.Object;
            Mock<IFunctionExecutor> functionExecutor = new Mock<IFunctionExecutor>(MockBehavior.Strict);

            functionExecutor.Setup(x => x.TryExecuteAsync(It.IsAny<IFunctionInstance>(), It.IsAny<CancellationToken>())).Returns(() =>
            {
                return Task.FromResult((IDelayedException)new DelayedException(new Exception("test")));
            });

            var result = await functionExecutor.Object.TryExecuteAsync(instanceFactory, loggerFactory, cancellationTokenSource.Token);
            Assert.NotNull(loggerProvider.GetAllLogMessages().SingleOrDefault(x => x.FormattedMessage == "Invocation cancelled - exiting retry loop."));
        }

        [Fact]
        public async Task TryExecuteWithRetries_Call_RetryNotifier()
        {
            Mock<IFunctionInstance> mockFunctionInstance = new Mock<IFunctionInstance>(MockBehavior.Strict);
            FunctionDescriptor functionDescriptor = GetFunctionDescriptor(100, 5);
            mockFunctionInstance.Setup(x => x.FunctionDescriptor).Returns(functionDescriptor);

            Func<IFunctionInstance> instanceFactory = () => mockFunctionInstance.Object;
            Mock<IFunctionExecutor> functionExecutor = new Mock<IFunctionExecutor>(MockBehavior.Strict);
            functionExecutor.Setup(x => x.TryExecuteAsync(It.IsAny<IFunctionInstance>(), It.IsAny<CancellationToken>())).Returns(() =>
            {
                return Task.FromResult((IDelayedException)new DelayedException(new Exception("test")));
            });
            int retryPendingCallsCount = 0, retryCompletedCallsCount = 0;
            functionExecutor.As<IRetryNotifier>().Setup(x => x.RetryPending()).Callback(() =>
            {
                retryPendingCallsCount++;
            });

            functionExecutor.As<IRetryNotifier>().Setup(x => x.RetryComplete()).Callback(() =>
            {
                retryCompletedCallsCount++;
            });
         
            var result = await functionExecutor.Object.TryExecuteAsync(instanceFactory, NullLoggerFactory.Instance, CancellationToken.None);

            Assert.Equal(retryPendingCallsCount, 1);
            Assert.Equal(retryCompletedCallsCount, 1);
        }

        private FunctionDescriptor GetFunctionDescriptor(int maxRetryCount, int delayInMs)
        {
            TimeSpan delay = TimeSpan.FromMilliseconds(delayInMs);
            var mockRetryStrategy = new Mock<IRetryStrategy>();
            mockRetryStrategy.Setup(p => p.MaxRetryCount).Returns(maxRetryCount);
            mockRetryStrategy.Setup(p => p.GetNextDelay(It.IsAny<RetryContext>())).Returns(delay);

            return new FunctionDescriptor()
            {
                RetryStrategy = mockRetryStrategy.Object
            };
        }
    }
}
