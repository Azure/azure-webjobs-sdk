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
    public class UtilityTests
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly TestLoggerProvider _loggerProvider;
        private readonly TestTraceWriter _traceWriter;

        public UtilityTests()
        {
            _loggerFactory = new LoggerFactory();
            var filter = new LogCategoryFilter();
            filter.DefaultLevel = LogLevel.Debug;
            _loggerProvider = new TestLoggerProvider(filter.Filter);
            _loggerFactory.AddProvider(_loggerProvider);

            _traceWriter = new TestTraceWriter(TraceLevel.Verbose);
        }

        [Fact]
        public void LogExceptionReceivedEvent_NonTransientEvent_LoggedAsError()
        {
            var ex = new MessageLockLostException("Lost the lock");
            Assert.False(ex.IsTransient);
            ExceptionReceivedEventArgs e = new ExceptionReceivedEventArgs(ex, "Complete");
            Utility.LogExceptionReceivedEvent(e, "Test", _traceWriter, _loggerFactory);

            var expectedMessage = $"Test error (Action=Complete)";
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
        public void LogExceptionReceivedEvent_TransientEvent_LoggedAsVerbose()
        {
            var ex = new MessagingCommunicationException("Test Path");
            Assert.True(ex.IsTransient);
            ExceptionReceivedEventArgs e = new ExceptionReceivedEventArgs(ex, "Connect");
            Utility.LogExceptionReceivedEvent(e, "Test", _traceWriter, _loggerFactory);

            var expectedMessage = $"Test error (Action=Connect)";
            var traceEvent = _traceWriter.GetTraces().Single();
            Assert.Equal(TraceLevel.Verbose, traceEvent.Level);
            Assert.Equal($"{expectedMessage} : {ex.ToString()}", traceEvent.Message);
            Assert.Same(ex, traceEvent.Exception);

            var logMessage = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Debug, logMessage.Level);
            Assert.Same(ex, logMessage.Exception);
            Assert.Equal(expectedMessage, logMessage.FormattedMessage);
        }

        [Fact]
        public void LogExceptionReceivedEvent_OperationCanceledException_LoggedAsVerbose()
        {
            var ex = new OperationCanceledException("Testing");
            ExceptionReceivedEventArgs e = new ExceptionReceivedEventArgs(ex, "Receive");
            Utility.LogExceptionReceivedEvent(e, "Test", _traceWriter, _loggerFactory);

            var expectedMessage = $"Test error (Action=Receive)";
            var traceEvent = _traceWriter.GetTraces().Single();
            Assert.Equal(TraceLevel.Verbose, traceEvent.Level);
            Assert.Equal($"{expectedMessage} : {ex.ToString()}", traceEvent.Message);
            Assert.Same(ex, traceEvent.Exception);

            var logMessage = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Debug, logMessage.Level);
            Assert.Same(ex, logMessage.Exception);
            Assert.Equal(expectedMessage, logMessage.FormattedMessage);
        }

        [Fact]
        public void LogExceptionReceivedEvent_NonMessagingException_LoggedAsError()
        {
            var ex = new MissingMethodException("What method??");
            ExceptionReceivedEventArgs e = new ExceptionReceivedEventArgs(ex, "Unknown");
            Utility.LogExceptionReceivedEvent(e, "Test", _traceWriter, _loggerFactory);

            var expectedMessage = $"Test error (Action=Unknown)";
            var traceEvent = _traceWriter.GetTraces().Single();
            Assert.Equal(TraceLevel.Error, traceEvent.Level);
            Assert.Same(ex, traceEvent.Exception);
            Assert.Equal(expectedMessage, traceEvent.Message);

            var logMessage = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Error, logMessage.Level);
            Assert.Same(ex, logMessage.Exception);
            Assert.Equal(expectedMessage, logMessage.FormattedMessage);
        }
    }
}
