﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Loggers
{
    public class ILoggerExtensionsTests
    {
        private Guid _invocationId = Guid.NewGuid();
        private DateTime _startTime = DateTime.Now;
        private DateTime _endTime;
        private string _triggerReason = "new queue message";
        private string _functionName = "TestFunction";
        private IDictionary<string, string> _arguments;

        public ILoggerExtensionsTests()
        {
            _endTime = _startTime.AddMilliseconds(450);
            _arguments = new Dictionary<string, string>
            {
                ["queueMessage"] = "my message",
                ["anotherParam"] = "some value"
            };
        }

        [Fact]
        public void LogFunctionResult_Succeeded_CreatesCorrectState()
        {
            int logCount = 0;
            ILogger logger = CreateMockLogger<LogValueCollection>((l, e, o, ex, f) =>
            {
                logCount++;
                Assert.Equal(LogLevel.Information, l);
                Assert.Equal(0, e);
                Assert.Null(ex);
                Assert.Equal($"Executed 'TestFunction' (Succeeded, Id={_invocationId})", f(o, ex));

                var payload = VerifyResultDefaultsAndConvert(o);
                Assert.True((bool)payload[LoggingKeys.Succeeded]);
                Assert.Equal("Executed '{Name}' (Succeeded, Id={InvocationId})", payload[LoggingKeys.OriginalFormat]);
            });

            var result = CreateDefaultInstanceLogEntry();

            logger.LogFunctionResult(result);

            Assert.Equal(1, logCount);
        }

        [Fact]
        public void LogFunctionResult_Failed_CreatesCorrectState()
        {
            int logCount = 0;
            ILogger logger = CreateMockLogger<LogValueCollection>((l, e, o, ex, f) =>
            {
                logCount++;
                Assert.Equal(LogLevel.Error, l);
                Assert.Equal(0, e);
                Assert.NotNull(ex);
                Assert.IsType<FunctionInvocationException>(ex);
                Assert.Equal($"Executed 'TestFunction' (Failed, Id={_invocationId})", f(o, ex));

                var payload = VerifyResultDefaultsAndConvert(o);
                Assert.False((bool)payload[LoggingKeys.Succeeded]);
                Assert.Equal("Executed '{Name}' (Failed, Id={InvocationId})", payload[LoggingKeys.OriginalFormat]);
            });

            var result = CreateDefaultInstanceLogEntry();

            var fex = new FunctionInvocationException("Failed");

            logger.LogFunctionResult(result, fex);

            Assert.Equal(1, logCount);
        }

        [Fact]
        public void LogFunctionResultAggregate_CreatesCorrectState()
        {
            DateTimeOffset now = DateTimeOffset.Now;
            int logCount = 0;
            ILogger logger = CreateMockLogger<LogValueCollection>((l, e, o, ex, f) =>
            {
                logCount++;
                Assert.Equal(LogLevel.Information, l);
                Assert.Equal(0, e);
                Assert.Null(ex);

                // nothing logged
                Assert.Equal(string.Empty, f(o, ex));

                // convert to dictionary                
                var payload = o.ToDictionary(k => k.Key, v => v.Value);

                Assert.Equal(10, payload.Count);
                Assert.Equal(_functionName, payload[LoggingKeys.Name]);
                Assert.Equal(4, payload[LoggingKeys.Failures]);
                Assert.Equal(116, payload[LoggingKeys.Successes]);
                Assert.Equal(200, payload[LoggingKeys.MinDuration]);
                Assert.Equal(2180, payload[LoggingKeys.MaxDuration]);
                Assert.Equal(340, payload[LoggingKeys.AvgDuration]);
                Assert.Equal(now, payload[LoggingKeys.Timestamp]);
                Assert.Equal(120, payload[LoggingKeys.Count]);
                Assert.Equal(96.67, payload[LoggingKeys.SuccessRate]);

                // {OriginalFormat} is still added, even though it is empty
                Assert.Equal(string.Empty, payload[LoggingKeys.OriginalFormat]);
            });

            var resultAggregate = new FunctionResultAggregate
            {
                Name = _functionName,
                Failures = 4,
                Successes = 116,
                MinMilliseconds = 200,
                MaxMilliseconds = 2180,
                AverageMilliseconds = 340,
                Timestamp = now
            };

            logger.LogFunctionResultAggregate(resultAggregate);

            Assert.Equal(1, logCount);
        }

        private FunctionInstanceLogEntry CreateDefaultInstanceLogEntry()
        {
            return new FunctionInstanceLogEntry
            {
                FunctionName = _functionName,
                FunctionInstanceId = _invocationId,
                StartTime = _startTime,
                EndTime = _endTime,
                LogOutput = "a bunch of output that we will not forward", // not used here -- this is all Traced
                TriggerReason = _triggerReason,
                ParentId = Guid.NewGuid(), // we do not track this
                ErrorDetails = null, // we do not use this -- we pass the exception in separately
                Arguments = _arguments
            };
        }

        private IDictionary<string, object> VerifyResultDefaultsAndConvert<TState>(TState state)
        {
            var enumerable = state as IEnumerable<KeyValuePair<string, object>>;
            Assert.NotNull(enumerable);

            var payload = enumerable.ToDictionary(k => k.Key, v => v.Value);

            Assert.Equal(10, payload.Count);
            Assert.Equal(_functionName, payload[LoggingKeys.Name]);
            Assert.Equal(_invocationId, payload[LoggingKeys.InvocationId]);
            Assert.Equal(_startTime, payload[LoggingKeys.StartTime]);
            Assert.Equal(_endTime, payload[LoggingKeys.EndTime]);
            Assert.Equal(TimeSpan.FromMilliseconds(450), payload[LoggingKeys.Duration]);
            Assert.Equal(_triggerReason, payload[LoggingKeys.TriggerReason]);

            // verify default arguments were passed with prefix
            var args = payload.Where(kvp => kvp.Value is string && kvp.Key.ToString().StartsWith(LoggingKeys.ParameterPrefix));
            Assert.Equal(_arguments.Count, args.Count());
            foreach (var arg in _arguments)
            {
                var payloadKey = LoggingKeys.ParameterPrefix + arg.Key;
                Assert.Equal(arg.Value, args.Single(kvp => kvp.Key == payloadKey).Value.ToString());
            }

            return payload;
        }

        private static ILogger CreateMockLogger<TState>(Action<LogLevel, EventId, TState, Exception, Func<TState, Exception, string>> callback)
        {
            var mockLogger = new Mock<ILogger>(MockBehavior.Strict);
            mockLogger
                .Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<TState>(), It.IsAny<Exception>(), It.IsAny<Func<TState, Exception, string>>()))
                .Callback<LogLevel, EventId, TState, Exception, Func<TState, Exception, string>>((l, e, o, ex, f) =>
                {
                    callback(l, e, o, ex, f);
                });

            return mockLogger.Object;
        }
    }
}
