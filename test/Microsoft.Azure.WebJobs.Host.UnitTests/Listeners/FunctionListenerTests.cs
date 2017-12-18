// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Listeners
{
    public class FunctionListenerTests
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly TestLoggerProvider _loggerProvider;

        CancellationToken ct = default(CancellationToken);

        FunctionDescriptor fd = new FunctionDescriptor()
        {
            ShortName = "testfunc"
        };

        public FunctionListenerTests()
        {
            _loggerFactory = new LoggerFactory();
            _loggerProvider = new TestLoggerProvider();
            _loggerFactory.AddProvider(_loggerProvider);
        }

        [Fact]
        public async Task FunctionListener_Throws_IfUnhandledListenerExceptionOnStartAsync()
        {
            Mock<IListener> badListener = new Mock<IListener>(MockBehavior.Strict);
            badListener.Setup(bl => bl.StartAsync(It.IsAny<CancellationToken>()))
                .Throws(new Exception("listener"));
            var listener = new FunctionListener(badListener.Object, fd, _loggerFactory);

            var e = await Assert.ThrowsAsync<FunctionListenerException>(async () => await listener.StartAsync(ct));

            // Validate Logger
            var loggerEx = _loggerProvider.CreatedLoggers.Single().LogMessages.Single().Exception as FunctionException;
            Assert.Equal("testfunc", loggerEx.MethodName);
            Assert.False(loggerEx.Handled);

            badListener.VerifyAll();
        }

        [Fact]
        public async Task FunctionListener_DoesNotThrow_IfHandled()
        {
            ILoggerFactory handlingLoggerFactory = new LoggerFactory();
            TestLoggerProvider handlingLoggerProvider = new TestLoggerProvider(null, (m) => (m.Exception as RecoverableException).Handled = true);
            handlingLoggerFactory.AddProvider(handlingLoggerProvider);

            Mock<IListener> badListener = new Mock<IListener>(MockBehavior.Strict);
            badListener.Setup(bl => bl.StartAsync(It.IsAny<CancellationToken>()))
                .Throws(new Exception("listener"));
            var listener = new FunctionListener(badListener.Object, fd, handlingLoggerFactory);

            await listener.StartAsync(ct);

            string expectedMessage = "The listener for function 'testfunc' was unable to start.";

            // Validate Logger
            var logMessage = handlingLoggerProvider.CreatedLoggers.Single().LogMessages.Single();
            Assert.Equal(expectedMessage, logMessage.FormattedMessage);
            var loggerEx = logMessage.Exception as FunctionException;
            Assert.Equal("testfunc", loggerEx.MethodName);
            Assert.True(loggerEx.Handled);

            badListener.VerifyAll();
        }

        [Fact]
        public async Task FunctionListener_DoesNotStop_IfNotStarted()
        {
            ILoggerFactory handlingLoggerFactory = new LoggerFactory();
            TestLoggerProvider handlingLoggerProvider = new TestLoggerProvider(null, (m) => (m.Exception as RecoverableException).Handled = true);
            handlingLoggerFactory.AddProvider(handlingLoggerProvider);

            Mock<IListener> badListener = new Mock<IListener>(MockBehavior.Strict);
            badListener.Setup(bl => bl.StartAsync(It.IsAny<CancellationToken>()))
                .Throws(new Exception("listener"));
            var listener = new FunctionListener(badListener.Object, fd, handlingLoggerFactory);

            await listener.StartAsync(ct);
            // these should do nothing, as function listener had an exception on start
            await listener.StopAsync(ct);
            await listener.StopAsync(ct);
            await listener.StopAsync(ct);

            // ensure that badListener.StopAsync is not called on a disabled function listener
            badListener.VerifyAll();
        }

        [Fact]
        public async Task FunctionListener_RunsStop_IfStarted()
        {
            Mock<IListener> goodListener = new Mock<IListener>(MockBehavior.Strict);
            goodListener.Setup(bl => bl.StartAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));
            goodListener.Setup(bl => bl.StopAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));
            var listener = new FunctionListener(goodListener.Object, fd, _loggerFactory);

            await listener.StartAsync(ct);
            await listener.StopAsync(ct);

            Assert.Empty(_loggerProvider.CreatedLoggers.Single().LogMessages);

            goodListener.VerifyAll();
        }
    }
}
