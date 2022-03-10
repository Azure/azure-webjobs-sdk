// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Loggers
{
    public class FunctionInstanceLoggerTests
    {
        private readonly FunctionInstanceLogger _instanceLogger;
        private readonly TestLoggerProvider _provider = new TestLoggerProvider();

        public FunctionInstanceLoggerTests()
        {
            ILoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(_provider);

            _instanceLogger = new FunctionInstanceLogger(loggerFactory);
        }

        [Fact]
        public void LogFunctionStarted_CallsLogger()
        {
            Dictionary<string, string> triggerDetails = new Dictionary<string, string>()
            {
                { "MessageId", Guid.NewGuid().ToString() },
                { "DequeueCount", "1" },
                { "InsertionTime", DateTime.Now.ToString() }
            };
            FunctionStartedMessage message = new FunctionStartedMessage
            {
                Function = new FunctionDescriptor
                {
                    FullName = "Host.Function.TestJob",
                    ShortName = "Function.TestJob",
                    LogName = "TestJob"
                },
                ReasonDetails = "TestReason",
                HostInstanceId = Guid.NewGuid(),
                FunctionInstanceId = Guid.NewGuid(),
                TriggerDetails = triggerDetails
            };

            string expectedMessage = $"MessageId: {triggerDetails["MessageId"]}, " +
                $"DequeueCount: {triggerDetails["DequeueCount"]}, " +
                $"InsertionTime: {triggerDetails["InsertionTime"]}";

            _instanceLogger.LogFunctionStarted(message);

            string expectedCategory = LogCategories.CreateFunctionCategory(message.Function.LogName);

            LogMessage[] logMessages = _provider.GetAllLogMessages().ToArray();

            Assert.Equal(2, logMessages.Length);

            LogMessage invocationLogMessage = logMessages[0];
            Assert.Equal(LogLevel.Information, invocationLogMessage.Level);
            Assert.Equal(expectedCategory, invocationLogMessage.Category);
            Assert.Equal($"Executing 'Function.TestJob' (Reason='TestReason', Id={message.FunctionInstanceId})", 
                invocationLogMessage.FormattedMessage);

            var state = invocationLogMessage.State.ToDictionary(p => p.Key, p => p.Value);
            Assert.Equal(4, state.Count);
            Assert.Equal(state["functionName"], message.Function.ShortName);
            Assert.Equal(state["reason"], message.ReasonDetails);
            Assert.Equal(state["invocationId"], message.FunctionInstanceId);

            LogMessage triggerDetailsLogMessage = logMessages[1];
            Assert.Equal(LogLevel.Information, triggerDetailsLogMessage.Level);
            Assert.Equal(expectedCategory, triggerDetailsLogMessage.Category);
            Assert.NotNull(triggerDetailsLogMessage.State);
            Assert.Equal($"Trigger Details: {expectedMessage}",
                triggerDetailsLogMessage.FormattedMessage);
        }

        [Fact]
        public void LogFunctionCompleted_CallsLogger()
        {
            FunctionDescriptor descriptor = new FunctionDescriptor
            {
                FullName = "Host.Function.TestJob",
                ShortName = "Function.TestJob",
                LogName = "TestJob"
            };
            FunctionCompletedMessage successMessage = new FunctionCompletedMessage
            {
                Function = descriptor,
                FunctionInstanceId = Guid.NewGuid(),
                HostInstanceId = Guid.NewGuid(),
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow + new TimeSpan(0, 0, 0, 0, 80)
            };

            Exception ex = new Exception("Kaboom!");
            FunctionCompletedMessage failureMessage = new FunctionCompletedMessage
            {
                Function = descriptor,
                Failure = new FunctionFailure { Exception = ex },
                FunctionInstanceId = new Guid("8d71c9e3-e809-4cfb-bb78-48ae25c7d26d"),
                HostInstanceId = Guid.NewGuid(),
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow + new TimeSpan(0, 0, 0, 0, 20)
            };

            _instanceLogger.LogFunctionCompleted(successMessage);
            _instanceLogger.LogFunctionCompleted(failureMessage);

            LogMessage[] logMessages = _provider.GetAllLogMessages().ToArray();

            Assert.Equal(2, logMessages.Length);

            string expectedCategory = LogCategories.CreateFunctionCategory(descriptor.LogName);

            // Success validation
            int expectedSuccessDurationMills = (int)successMessage.EndTime.Subtract(successMessage.StartTime).TotalMilliseconds;
            LogMessage logMessage = logMessages[0];
            Assert.Equal(LogLevel.Information, logMessage.Level);
            Assert.Equal(expectedCategory, logMessage.Category);
            Assert.Equal($"Executed 'Function.TestJob' (Succeeded, Id={successMessage.FunctionInstanceId}, Duration={expectedSuccessDurationMills}ms)", logMessage.FormattedMessage);

            var state = logMessage.State.ToDictionary(p => p.Key, p => p.Value);
            Assert.Equal(5, state.Count);
            Assert.Equal(state["functionName"], descriptor.ShortName);
            Assert.Equal(state["invocationId"], successMessage.FunctionInstanceId);
            Assert.Equal(state["status"], "Succeeded");
            Assert.Equal(state["executionDuration"], expectedSuccessDurationMills);

            // Failure validation
            int expectedFailureDurationMills = (int)failureMessage.EndTime.Subtract(failureMessage.StartTime).TotalMilliseconds;
            logMessage = logMessages[1];
            Assert.Equal(LogLevel.Error, logMessage.Level);
            Assert.Equal(expectedCategory, logMessage.Category);
            Assert.Equal($"Executed 'Function.TestJob' (Failed, Id={failureMessage.FunctionInstanceId}, Duration={expectedFailureDurationMills}ms)", logMessage.FormattedMessage);
            Assert.Same(ex, logMessage.Exception);

            state = logMessage.State.ToDictionary(p => p.Key, p => p.Value);
            Assert.Equal(5, state.Count);
            Assert.Equal(state["functionName"], descriptor.ShortName);
            Assert.Equal(state["invocationId"], failureMessage.FunctionInstanceId);
            Assert.Equal(state["status"], "Failed");
            Assert.Equal(state["executionDuration"], expectedFailureDurationMills);
        }
    }
}
