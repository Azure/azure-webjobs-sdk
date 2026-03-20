// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Scale
{
    public class ScaleLoggerExtensionTests
    {
        private readonly TestLoggerProvider _loggerProvider;
        private readonly ILogger _logger;

        public ScaleLoggerExtensionTests()
        {
            _loggerProvider = new TestLoggerProvider();
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(_loggerProvider);
            _logger = loggerFactory.CreateLogger<ScaleLoggerExtensionTests>();
        }

        #region LogFunctionScaleVote Tests

        [Fact]
        public void LogFunctionScaleVote_Simple_LogsAtInformationLevel()
        {
            _logger.LogFunctionScaleVote("myFunction", "ScaleOut");

            var log = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Information, log.Level);
            Assert.Equal(8002, log.EventId.Id);
            Assert.Equal("LogFunctionScaleVote", log.EventId.Name);
            Assert.Equal("Function 'myFunction' vote: 'ScaleOut'.", log.FormattedMessage);
        }

        [Fact]
        public void LogFunctionScaleVote_SimpleWithReason_LogsAtInformationLevel()
        {
            _logger.LogFunctionScaleVote("myFunction", "ScaleIn", "All queues empty");

            var log = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Information, log.Level);
            Assert.Equal(8002, log.EventId.Id);
            Assert.Equal("LogFunctionScaleVote", log.EventId.Name);
            Assert.Equal("Function 'myFunction' vote: 'ScaleIn'. All queues empty", log.FormattedMessage);
        }

        [Fact]
        public void LogFunctionScaleVote_SimpleWithNullReason_OmitsReason()
        {
            _logger.LogFunctionScaleVote("myFunction", "None", null);

            var log = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Information, log.Level);
            Assert.Equal(8002, log.EventId.Id);
            Assert.Equal("Function 'myFunction' vote: 'None'.", log.FormattedMessage);
        }

        [Fact]
        public void LogFunctionScaleVote_Detailed_LogsAtInformationLevel()
        {
            _logger.LogFunctionScaleVote("myFunction", 5, 100, 20);

            var log = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Information, log.Level);
            Assert.Equal(8002, log.EventId.Id);
            Assert.Equal("LogFunctionScaleVote", log.EventId.Name);
            Assert.Equal("Function 'myFunction' vote: TargetWorkerCount='5', QueueLength='100', Concurrency='20'.", log.FormattedMessage);
        }

        [Fact]
        public void LogFunctionScaleVote_DetailedWithReason_LogsAtInformationLevel()
        {
            _logger.LogFunctionScaleVote("myFunction", 3, 50, 10, "EntityPath='myqueue'");

            var log = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Information, log.Level);
            Assert.Equal(8002, log.EventId.Id);
            Assert.Equal("Function 'myFunction' vote: TargetWorkerCount='3', QueueLength='50', Concurrency='10'. EntityPath='myqueue'", log.FormattedMessage);
        }

        [Fact]
        public void LogFunctionScaleVote_DetailedWithNullReason_OmitsReason()
        {
            _logger.LogFunctionScaleVote("myFunction", 1, 10, 5, null);

            var log = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Information, log.Level);
            Assert.Equal(8002, log.EventId.Id);
            Assert.Equal("Function 'myFunction' vote: TargetWorkerCount='1', QueueLength='10', Concurrency='5'.", log.FormattedMessage);
        }

        #endregion

        #region LogFunctionScaleError Tests

        [Fact]
        public void LogFunctionScaleError_LogsAtErrorLevel()
        {
            var exception = new InvalidOperationException("Something broke");
            _logger.LogFunctionScaleError("Failed to get vote.", "myFunction", exception);

            var log = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Error, log.Level);
            Assert.Equal(8001, log.EventId.Id);
            Assert.Equal("FunctionScaleError", log.EventId.Name);
            Assert.Equal("Function 'myFunction' error: Failed to get vote.", log.FormattedMessage);
            Assert.Same(exception, log.Exception);
        }

        #endregion

        #region LogFunctionScaleWarning Tests

        [Fact]
        public void LogFunctionScaleWarning_LogsAtWarningLevel()
        {
            var exception = new TimeoutException("Connection timed out");
            _logger.LogFunctionScaleWarning("Error querying for scale status", "myFunction", exception);

            var log = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Warning, log.Level);
            Assert.Equal(8003, log.EventId.Id);
            Assert.Equal("FunctionScaleWarning", log.EventId.Name);
            Assert.Equal("Function 'myFunction' warning: Error querying for scale status", log.FormattedMessage);
            Assert.Same(exception, log.Exception);
        }

        [Fact]
        public void LogFunctionScaleWarning_WithoutException_LogsAtWarningLevel()
        {
            _logger.LogFunctionScaleWarning("Partition count is zero", "myFunction");

            var log = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Warning, log.Level);
            Assert.Equal(8003, log.EventId.Id);
            Assert.Equal("FunctionScaleWarning", log.EventId.Name);
            Assert.Equal("Function 'myFunction' warning: Partition count is zero", log.FormattedMessage);
            Assert.Null(log.Exception);
        }

        #endregion

        #region EventId Uniqueness Tests

        [Fact]
        public void EventIds_AreUnique()
        {
            // Verify vote, error, and warning use distinct EventIds
            var exception = new Exception("test");

            _logger.LogFunctionScaleVote("fn", "ScaleOut");
            _logger.LogFunctionScaleError("error msg", "fn", exception);
            _logger.LogFunctionScaleWarning("warning msg", "fn", exception);

            var logs = _loggerProvider.GetAllLogMessages().ToList();
            Assert.Equal(3, logs.Count);

            Assert.Equal(8002, logs[0].EventId.Id); // Vote
            Assert.Equal(8001, logs[1].EventId.Id); // Error
            Assert.Equal(8003, logs[2].EventId.Id); // Warning

            // All EventIds are distinct
            var eventIds = logs.Select(l => l.EventId.Id).Distinct().ToList();
            Assert.Equal(3, eventIds.Count);
        }

        #endregion
    }
}
