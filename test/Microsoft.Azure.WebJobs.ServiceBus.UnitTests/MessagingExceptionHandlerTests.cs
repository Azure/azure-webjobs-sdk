// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceBus.Messaging;
using Xunit;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests
{
    public class MessagingExceptionHandlerTests
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly TestLoggerProvider _loggerProvider;
        private readonly TestTraceWriter _traceWriter;

        public MessagingExceptionHandlerTests()
        {
            _loggerFactory = new LoggerFactory();
            var filter = new LogCategoryFilter();
            filter.DefaultLevel = LogLevel.Debug;
            _loggerProvider = new TestLoggerProvider(filter.Filter);
            _loggerFactory.AddProvider(_loggerProvider);

            _traceWriter = new TestTraceWriter(TraceLevel.Verbose);
        }

        [Fact]
        public void ServiceBus_LogExceptionReceivedEvent_NonTransientEvent_LoggedAsError()
        {
            var ex = new MessageLockLostException("Lost the lock");
            Assert.False(ex.IsTransient);
            ExceptionReceivedEventArgs e = new ExceptionReceivedEventArgs(ex, "Complete");
            var options = new OnMessageOptions();
            var handler = MessagingExceptionHandler.Subscribe(options, _traceWriter, _loggerFactory);
            handler.LogExceptionReceivedEvent(e);

            var expectedMessage = $"MessageReceiver error (Action=Complete) : {e.Exception.ToString()}";
            var traceEvent = _traceWriter.GetTraces().Single();
            Assert.Equal(TraceLevel.Error, traceEvent.Level);
            Assert.Same(ex, traceEvent.Exception);
            Assert.Equal(expectedMessage, traceEvent.Message);

            var logMessage = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Error, logMessage.Level);
            Assert.Same(ex, logMessage.Exception);
            Assert.Equal(expectedMessage, logMessage.FormattedMessage);
        }

        [Fact]
        public void ServiceBus_LogExceptionReceivedEvent_TransientEvent_LoggedAsVerbose()
        {
            var ex = new MessagingCommunicationException("Test Path");
            Assert.True(ex.IsTransient);
            ExceptionReceivedEventArgs e = new ExceptionReceivedEventArgs(ex, "Connect");
            var options = new OnMessageOptions();
            var handler = MessagingExceptionHandler.Subscribe(options, _traceWriter, _loggerFactory);
            handler.LogExceptionReceivedEvent(e);

            var expectedMessage = $"MessageReceiver error (Action=Connect) : {ex.ToString()}";
            var traceEvent = _traceWriter.GetTraces().Single();
            Assert.Equal(TraceLevel.Info, traceEvent.Level);
            Assert.Equal(expectedMessage, traceEvent.Message);
            Assert.Same(ex, traceEvent.Exception);

            var logMessage = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Information, logMessage.Level);
            Assert.Same(ex, logMessage.Exception);
            Assert.Equal(expectedMessage, logMessage.FormattedMessage);
        }

        [Fact]
        public void ServiceBus_LogExceptionReceivedEvent_OperationCanceledException_LoggedAsVerbose()
        {
            var ex = new OperationCanceledException("Testing");
            ExceptionReceivedEventArgs e = new ExceptionReceivedEventArgs(ex, "Receive");
            var options = new OnMessageOptions();
            var handler = MessagingExceptionHandler.Subscribe(options, _traceWriter, _loggerFactory);
            handler.LogExceptionReceivedEvent(e);

            var expectedMessage = $"MessageReceiver error (Action=Receive) : {ex.ToString()}";
            var traceEvent = _traceWriter.GetTraces().Single();
            Assert.Equal(TraceLevel.Info, traceEvent.Level);
            Assert.Equal(expectedMessage, traceEvent.Message);
            Assert.Same(ex, traceEvent.Exception);

            var logMessage = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Information, logMessage.Level);
            Assert.Same(ex, logMessage.Exception);
            Assert.Equal(expectedMessage, logMessage.FormattedMessage);
        }

        [Fact]
        public void ServiceBus_LogExceptionReceivedEvent_NonMessagingException_LoggedAsError()
        {
            var ex = new MissingMethodException("What method??");
            ExceptionReceivedEventArgs e = new ExceptionReceivedEventArgs(ex, "Unknown");
            var options = new OnMessageOptions();
            var handler = MessagingExceptionHandler.Subscribe(options, _traceWriter, _loggerFactory);
            handler.LogExceptionReceivedEvent(e);

            var expectedMessage = $"MessageReceiver error (Action=Unknown) : {e.Exception.ToString()}";
            var traceEvent = _traceWriter.GetTraces().Single();
            Assert.Equal(TraceLevel.Error, traceEvent.Level);
            Assert.Same(ex, traceEvent.Exception);
            Assert.Equal(expectedMessage, traceEvent.Message);

            var logMessage = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Error, logMessage.Level);
            Assert.Same(ex, logMessage.Exception);
            Assert.Equal(expectedMessage, logMessage.FormattedMessage);
        }

        [Fact]
        public void EventHub_LogExceptionReceivedEvent_PartitionExceptions_LoggedAsTrace()
        {
            var ex = new ReceiverDisconnectedException("New receiver with higher epoch of '30402' is created hence current receiver with epoch '30402' is getting disconnected.");
            ExceptionReceivedEventArgs e = new ExceptionReceivedEventArgs(ex, "Receive");
            var options = new EventProcessorOptions();
            var handler = MessagingExceptionHandler.Subscribe(options, _traceWriter, _loggerFactory);
            handler.LogExceptionReceivedEvent(e);

            var expectedMessage = $"EventProcessorHost error (Action=Receive) : {ex.ToString()}";
            var traceEvent = _traceWriter.GetTraces().Single();
            Assert.Equal(TraceLevel.Info, traceEvent.Level);
            Assert.Same(ex, traceEvent.Exception);
            Assert.Equal(expectedMessage, traceEvent.Message);

            var logMessage = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Information, logMessage.Level);
            Assert.Same(ex, logMessage.Exception);
            Assert.Equal(expectedMessage, logMessage.FormattedMessage);
        }
    }
}
