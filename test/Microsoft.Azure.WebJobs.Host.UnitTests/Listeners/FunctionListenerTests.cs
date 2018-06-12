// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
        CancellationToken ct = default(CancellationToken);

        FunctionDescriptor fd = new FunctionDescriptor()
        {
            ShortName = "testfunc"
        };

        private readonly ILoggerFactory _loggerFactory;
        private readonly TestLoggerProvider _loggerProvider;

        public FunctionListenerTests()
        {
            _loggerFactory = new LoggerFactory();
            _loggerProvider = new TestLoggerProvider();
            _loggerFactory.AddProvider(_loggerProvider);
        }

        [Fact]
        public async Task FunctionListener_RetriesOnListenerFailure_WhenPartialHostStartupEnabled()
        {
            var trace = new TestTraceWriter(TraceLevel.Error);
            Mock<IListener> badListener = new Mock<IListener>(MockBehavior.Strict);
            int failureCount = 0;
            badListener.Setup(bl => bl.StartAsync(It.IsAny<CancellationToken>()))
                .Callback<CancellationToken>((ct) =>
                {
                    if (failureCount++ < 3)
                    {
                        throw new Exception("Listener Exploded!");
                    }
                })
                .Returns(Task.CompletedTask);
            badListener.Setup(bl => bl.StopAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var listener = new FunctionListener(badListener.Object, fd, trace, _loggerFactory, allowPartialHostStartup: true, minRetryInterval: TimeSpan.FromMilliseconds(10), maxRetryInterval: TimeSpan.FromMilliseconds(100));

            // we should return right away with the listener
            // attempting to restart in the background
            await listener.StartAsync(ct);

            await TestHelpers.Await(() =>
            {
                var lastTrace = trace.GetTraces().Last();
                return lastTrace.Message == "Listener successfully started for function 'testfunc' after 3 retries.";
            });

            badListener.Verify(p => p.StartAsync(It.IsAny<CancellationToken>()), Times.Exactly(4));

            var validators = new Action<string>[]
            {
                p => Assert.Equal("The listener for function 'testfunc' was unable to start.", p),
                p => Assert.Equal("Retrying to start listener for function 'testfunc' (Attempt 1)", p),
                p => Assert.Equal("The listener for function 'testfunc' was unable to start.", p),
                p => Assert.Equal("Retrying to start listener for function 'testfunc' (Attempt 2)", p),
                p => Assert.Equal("The listener for function 'testfunc' was unable to start.", p),
                p => Assert.Equal("Retrying to start listener for function 'testfunc' (Attempt 3)", p),
                p => Assert.Equal("Listener successfully started for function 'testfunc' after 3 retries.", p)
            };
            Assert.Collection(trace.GetTraces().Select(p => p.Message), validators);

            // Validate Logger
            var logs = _loggerProvider.CreatedLoggers.Single().GetLogMessages().Select(p => p.FormattedMessage);
            Assert.Collection(logs, validators);

            await listener.StopAsync(ct);
            badListener.Verify(p => p.StopAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task FunctionListener_BackgroundRetriesStopped_WhenListenerStopped()
        {
            await RetryStopTestHelper(
                (listener) => listener.StopAsync(CancellationToken.None)
            );
        }

        [Fact]
        public async Task FunctionListener_BackgroundRetriesStopped_WhenListenerCancelled()
        {
            await RetryStopTestHelper(
                (listener) =>
                {
                    listener.Cancel();
                    return Task.CompletedTask;
                });
        }

        [Fact]
        public async Task FunctionListener_BackgroundRetriesStopped_WhenListenerDisposed()
        {
            await RetryStopTestHelper(
                (listener) =>
                {
                    listener.Dispose();
                    return Task.CompletedTask;
                });
        }

        private async Task RetryStopTestHelper(Func<FunctionListener, Task> action)
        {
            var trace = new TestTraceWriter(TraceLevel.Error);
            Mock<IListener> badListener = new Mock<IListener>(MockBehavior.Strict);
            badListener.Setup(bl => bl.StartAsync(It.IsAny<CancellationToken>()))
                .Callback<CancellationToken>((ct) =>
                {
                    throw new Exception("Listener Exploded!");
                })
                .Returns(Task.CompletedTask);
            badListener.Setup(bl => bl.Dispose());
            badListener.Setup(bl => bl.Cancel());
            badListener.Setup(bl => bl.StopAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var listener = new FunctionListener(badListener.Object, fd, trace, _loggerFactory, allowPartialHostStartup: true, minRetryInterval: TimeSpan.FromMilliseconds(10), maxRetryInterval: TimeSpan.FromMilliseconds(100));

            await listener.StartAsync(ct);

            // wait until we're sure the retry task is running
            await TestHelpers.Await(() =>
            {
                var lastTrace = trace.GetTraces().Last();
                return trace.GetTraces().Any(p => p.Message.Contains("Retrying to start listener"));
            });

            // initiate the action which should stop the retry task
            await action(listener);

            // take a count before and after a delay to make sure the
            // task is no longer running
            int prevRetryCount = trace.GetTraces().Count(p => p.Message.Contains("Retrying to start listener"));
            await Task.Delay(1000);
            int retryCount = trace.GetTraces().Count(p => p.Message.Contains("Retrying to start listener"));

            Assert.Equal(prevRetryCount, retryCount);
        }

        [Fact]
        public async Task FunctionListener_ConcurrentStartStop_ListenerIsStopped()
        {
            var trace = new TestTraceWriter(TraceLevel.Error);
            Mock<IListener> badListener = new Mock<IListener>(MockBehavior.Strict);
            var listener = new FunctionListener(badListener.Object, fd, trace, _loggerFactory, allowPartialHostStartup: true, minRetryInterval: TimeSpan.FromMilliseconds(10), maxRetryInterval: TimeSpan.FromMilliseconds(100));

            int count = 0;
            badListener.Setup(bl => bl.StartAsync(It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    count++;

                    if (count == 1)
                    {
                        // initiate the restart loop by falling on the first attemt
                        throw new Exception("Listener Exploded!");
                    }
                    else if (count == 2)
                    {
                        // while we're in the retry loop simulate a concurrent stop by
                        // invoking stop ourselves
                        await listener.StopAsync(ct);
                    }
                    else
                    {
                        // shouldn't get to here
                    }
                });

            bool stopCalled = false;
            badListener.Setup(bl => bl.StopAsync(It.IsAny<CancellationToken>()))
                .Callback(() => stopCalled = true)
                .Returns(Task.CompletedTask);

            await listener.StartAsync(ct);

            await TestHelpers.Await(() =>
            {
                return stopCalled;
            }, timeout: 4000, userMessageCallback: () => "Listener not stopped.");

            badListener.Verify(p => p.StartAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
            badListener.Verify(p => p.StopAsync(It.IsAny<CancellationToken>()), Times.Exactly(1));

            Assert.Collection(trace.GetTraces().Select(p => p.Message),
                p => Assert.Equal("The listener for function 'testfunc' was unable to start.", p),
                p => Assert.Equal("Retrying to start listener for function 'testfunc' (Attempt 1)", p),
                p => Assert.Equal("Listener successfully started for function 'testfunc' after 1 retries.", p),
                p => Assert.Equal("Listener for function 'testfunc' stopped. A stop was initiated while starting.", p));

            // make sure the retry loop is not running
            await Task.Delay(1000);
            Assert.Equal(2, count);
        }

        [Fact]
        public async Task StopAsync_MultipleConcurrentRequests_InnerListenerStoppedOnce()
        {
            var trace = new TestTraceWriter(TraceLevel.Error);
            Mock<IListener> innerListener = new Mock<IListener>(MockBehavior.Strict);
            var functionListener = new FunctionListener(innerListener.Object, fd, trace, _loggerFactory);

            innerListener.Setup(bl => bl.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            innerListener.Setup(bl => bl.StopAsync(It.IsAny<CancellationToken>())).Returns(async () => await Task.Delay(100));

            await functionListener.StartAsync(CancellationToken.None);

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(functionListener.StopAsync(CancellationToken.None));
            }

            await Task.WhenAll(tasks);

            innerListener.Verify(p => p.StopAsync(It.IsAny<CancellationToken>()), Times.Exactly(1));
        }

        [Fact]
        public async Task FunctionListener_Throws_IfUnhandledListenerExceptionOnStartAsync()
        {
            var trace = new TestTraceWriter(TraceLevel.Error);
            Mock<IListener> badListener = new Mock<IListener>(MockBehavior.Strict);
            badListener.Setup(bl => bl.StartAsync(It.IsAny<CancellationToken>()))
                .Throws(new Exception("listener"));
            var listener = new FunctionListener(badListener.Object, fd, trace, _loggerFactory);

            var e = await Assert.ThrowsAsync<FunctionListenerException>(async () => await listener.StartAsync(ct));

            // Validate TraceWriter
            var traceEx = trace.GetTraces()[0].Exception as FunctionException;
            Assert.Equal("testfunc", traceEx.MethodName);
            Assert.False(traceEx.Handled);

            // Validate Logger
            var loggerEx = _loggerProvider.CreatedLoggers.Single().GetLogMessages().Single().Exception as FunctionException;
            Assert.Equal("testfunc", loggerEx.MethodName);
            Assert.False(loggerEx.Handled);

            badListener.VerifyAll();
        }

        [Fact]
        public async Task FunctionListener_DoesNotThrow_IfHandled()
        {
            HandlingTraceWriter trace = new HandlingTraceWriter(TraceLevel.Error, (te) => (te.Exception as RecoverableException).Handled = true);
            Mock<IListener> badListener = new Mock<IListener>(MockBehavior.Strict);
            badListener.Setup(bl => bl.StartAsync(It.IsAny<CancellationToken>()))
                .Throws(new Exception("listener"));
            var listener = new FunctionListener(badListener.Object, fd, trace, _loggerFactory);

            await listener.StartAsync(ct);

            string expectedMessage = "The listener for function 'testfunc' was unable to start.";

            // Validate TraceWriter
            Assert.Equal(expectedMessage, trace.Traces[0].Message);
            var traceEx = trace.Traces[0].Exception as FunctionException;
            Assert.Equal("testfunc", traceEx.MethodName);
            Assert.True(traceEx.Handled);

            // Validate Logger
            var logMessage = _loggerProvider.CreatedLoggers.Single().GetLogMessages().Single();
            Assert.Equal(expectedMessage, logMessage.FormattedMessage);
            var loggerEx = logMessage.Exception as FunctionException;
            Assert.Equal("testfunc", loggerEx.MethodName);
            Assert.True(loggerEx.Handled);

            badListener.VerifyAll();
        }

        [Fact]
        public async Task FunctionListener_DoesNotStop_IfNotStarted()
        {
            HandlingTraceWriter trace = new HandlingTraceWriter(TraceLevel.Error, (te) => (te.Exception as RecoverableException).Handled = true);
            Mock<IListener> badListener = new Mock<IListener>(MockBehavior.Strict);
            badListener.Setup(bl => bl.StartAsync(It.IsAny<CancellationToken>()))
                .Throws(new Exception("listener"));
            var listener = new FunctionListener(badListener.Object, fd, trace, _loggerFactory);

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
            HandlingTraceWriter trace = new HandlingTraceWriter(TraceLevel.Error, (te) => (te.Exception as RecoverableException).Handled = true);
            Mock<IListener> goodListener = new Mock<IListener>(MockBehavior.Strict);
            goodListener.Setup(bl => bl.StartAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));
            goodListener.Setup(bl => bl.StopAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));
            var listener = new FunctionListener(goodListener.Object, fd, trace, _loggerFactory);

            await listener.StartAsync(ct);
            await listener.StopAsync(ct);

            Assert.Empty(trace.Traces);
            Assert.Empty(_loggerProvider.CreatedLoggers.Single().GetLogMessages());

            goodListener.VerifyAll();
        }

        private class HandlingTraceWriter : TraceWriter
        {
            public Collection<TraceEvent> Traces = new Collection<TraceEvent>();
            public Action<TraceEvent> _handler;

            public HandlingTraceWriter(TraceLevel level, Action<TraceEvent> handler) : base(level)
            {
                _handler = handler;
            }

            public override void Trace(TraceEvent traceEvent)
            {
                Traces.Add(traceEvent);
                _handler(traceEvent);
            }
        }
    }
}
