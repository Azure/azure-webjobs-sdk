// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        public async Task LogFunctionStarted_CallsLogger()
        {
            FunctionStartedMessage message = new FunctionStartedMessage
            {
                Function = new FunctionDescriptor
                {
                    ShortName = "Function.TestJob",
                    LogName = "TestJob"
                },
                ReasonDetails = "TestReason",
                HostInstanceId = Guid.NewGuid(),
                FunctionInstanceId = Guid.NewGuid()
            };

            await _instanceLogger.LogFunctionStartedAsync(message, CancellationToken.None);

            string expectedCategory = LogCategories.CreateFunctionCategory(message.Function.LogName);

            LogMessage logMessage = _provider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Information, logMessage.Level);
            Assert.Equal(expectedCategory, logMessage.Category);

            Assert.Equal($"Executing '{message.Function.ShortName}' (Reason='TestReason', Id={message.FunctionInstanceId})", logMessage.FormattedMessage);
            VerifyLogMessageState(logMessage, message.FunctionInstanceId.ToString());
        }

        [Fact]
        public async Task LogFunctionCompleted_CallsLogger()
        {
            FunctionDescriptor descriptor = new FunctionDescriptor
            {
                ShortName = "Function.TestJob",
                LogName = "TestJob"
            };
            FunctionCompletedMessage successMessage = new FunctionCompletedMessage
            {
                Function = descriptor,
                FunctionInstanceId = Guid.NewGuid(),
                HostInstanceId = Guid.NewGuid()
            };

            Exception ex = new Exception("Kaboom!");
            FunctionCompletedMessage failureMessage = new FunctionCompletedMessage
            {
                Function = descriptor,
                Failure = new FunctionFailure { Exception = ex },
                FunctionInstanceId = new Guid("8d71c9e3-e809-4cfb-bb78-48ae25c7d26d"),
                HostInstanceId = Guid.NewGuid()
            };

            await _instanceLogger.LogFunctionCompletedAsync(successMessage, CancellationToken.None);
            await _instanceLogger.LogFunctionCompletedAsync(failureMessage, CancellationToken.None);

            LogMessage[] logMessages = _provider.GetAllLogMessages().ToArray();

            Assert.Equal(2, logMessages.Length);

            string expectedCategory = LogCategories.CreateFunctionCategory("TestJob");

            LogMessage logMessage = logMessages[0];
            Assert.Equal(LogLevel.Information, logMessage.Level);
            Assert.Equal(expectedCategory, logMessage.Category);
            Assert.Equal($"Executed '{failureMessage.Function.ShortName}' (Succeeded, Id={successMessage.FunctionInstanceId})", logMessage.FormattedMessage);
            VerifyLogMessageState(logMessage, successMessage.FunctionInstanceId.ToString());

            logMessage = logMessages[1];
            Assert.Equal(LogLevel.Error, logMessage.Level);
            Assert.Equal(expectedCategory, logMessage.Category);
            Assert.Equal($"Executed '{failureMessage.Function.ShortName}' (Failed, Id={failureMessage.FunctionInstanceId})", logMessage.FormattedMessage);
            Assert.Same(ex, logMessage.Exception);
            VerifyLogMessageState(logMessage, failureMessage.FunctionInstanceId.ToString());
        }

        private static void VerifyLogMessageState(LogMessage logMessage, string functionInvocationId)
        {
            if (logMessage.State is IEnumerable<KeyValuePair<string, object>> stateDict)
            {
                var kvps = stateDict.Where(k => string.Equals(k.Key, LogConstants.LogSummaryKey, StringComparison.OrdinalIgnoreCase)).LastOrDefault();
                Assert.NotNull(kvps);
                kvps = stateDict.Where(k => string.Equals(k.Key, ScopeKeys.FunctionInvocationId, StringComparison.OrdinalIgnoreCase)).LastOrDefault();
                Assert.NotNull(kvps);
                Assert.True(functionInvocationId ==(string) kvps.Value);
            }
        }
    }
}
