// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Loggers
{
    public class AppInsightsLoggerTests
    {
        private readonly Guid _invocationId = Guid.NewGuid();
        private readonly DateTime _startTime = DateTime.UtcNow;
        private readonly DateTime _endTime;
        private readonly string _triggerReason = "new queue message";
        private readonly string _functionName = "TestFunction";
        private readonly IDictionary<string, string> _arguments;
        private readonly TestTelemetryChannel _channel = new TestTelemetryChannel();
        private readonly string defaultIp = "0.0.0.0";
        private readonly TelemetryClient _client;

        public AppInsightsLoggerTests()
        {
            _endTime = _startTime.AddMilliseconds(450);
            _arguments = new Dictionary<string, string>
            {
                ["queueMessage"] = "my message",
                ["anotherParam"] = "some value"
            };

            TelemetryConfiguration config = new TelemetryConfiguration
            {
                TelemetryChannel = _channel,
                InstrumentationKey = "some key"
            };
            _client = new TelemetryClient(config);
        }

        [Fact]
        public void LogFunctionResult_Succeeded_SendsCorrectTelemetry()
        {
            var result = CreateDefaultInstanceLogEntry();
            ILogger logger = CreateLogger(LoggingCategories.Results);
            logger.LogFunctionResult(result);

            RequestTelemetry telemetry = _channel.Telemetries.Single() as RequestTelemetry;

            Assert.Equal(_invocationId.ToString(), telemetry.Id);
            Assert.Equal(_invocationId.ToString(), telemetry.Context.Operation.Id);
            Assert.Equal(_functionName, telemetry.Name);
            Assert.Equal(_functionName, telemetry.Context.Operation.Name);
            Assert.Equal(defaultIp, telemetry.Context.Location.Ip);
            // TODO: Beef up validation to include properties
        }

        [Fact]
        public void LogFunctionResult_Failed_SendsCorrectTelemetry()
        {
            var result = CreateDefaultInstanceLogEntry();
            FunctionInvocationException fex = new FunctionInvocationException("Failed");
            ILogger logger = CreateLogger(LoggingCategories.Results);
            logger.LogFunctionResult(result, fex);

            // Errors log an associated Exception
            RequestTelemetry requestTelemetry = _channel.Telemetries.OfType<RequestTelemetry>().Single();
            ExceptionTelemetry exceptionTelemetry = _channel.Telemetries.OfType<ExceptionTelemetry>().Single();

            Assert.Equal(2, _channel.Telemetries.Count);
            Assert.Equal(_invocationId.ToString(), requestTelemetry.Id);
            Assert.Equal(_invocationId.ToString(), requestTelemetry.Context.Operation.Id);
            Assert.Equal(_functionName, requestTelemetry.Name);
            Assert.Equal(_functionName, requestTelemetry.Context.Operation.Name);
            Assert.Equal(defaultIp, requestTelemetry.Context.Location.Ip);
            // TODO: Beef up validation to include properties

            // Exception needs to have associated id
            Assert.Equal(_invocationId.ToString(), exceptionTelemetry.Context.Operation.Id);
            Assert.Equal(_functionName, exceptionTelemetry.Context.Operation.Name);
            Assert.Same(fex, exceptionTelemetry.Exception);
            // TODO: Beef up validation to include properties
        }

        [Fact]
        public void LogFunctionAggregate_SendsCorrectTelemetry()
        {
            DateTime now = DateTime.UtcNow;
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

            ILogger logger = CreateLogger(LoggingCategories.Aggregator);
            logger.LogFunctionResultAggregate(resultAggregate);

            EventTelemetry eventTelemetry = _channel.Telemetries.Single() as EventTelemetry;

            Assert.Equal($"{_functionName}.Aggregation", eventTelemetry.Name);
            Assert.Equal(7, eventTelemetry.Metrics.Count);
            Assert.Equal(4, eventTelemetry.Metrics[LoggingKeys.Failures]);
            Assert.Equal(116, eventTelemetry.Metrics[LoggingKeys.Successes]);
            Assert.Equal(200, eventTelemetry.Metrics[LoggingKeys.MinDuration]);
            Assert.Equal(2180, eventTelemetry.Metrics[LoggingKeys.MaxDuration]);
            Assert.Equal(340, eventTelemetry.Metrics[LoggingKeys.AvgDuration]);
            Assert.Equal(96.67, eventTelemetry.Metrics[LoggingKeys.SuccessRate]);
            Assert.Equal(120, eventTelemetry.Metrics[LoggingKeys.Count]);
        }

        [Fact]
        public void LogFunctionResult_HttpRequest_SendsCorrectTelemetry()
        {
            // If the scope has an HttpRequestMessage, we'll use the proper values
            // for the RequestTelemetry
            DateTime now = DateTime.UtcNow;
            var result = CreateDefaultInstanceLogEntry();

            var request = new HttpRequestMessage(HttpMethod.Post, "http://someuri/api/path");
            request.Headers.Add("User-Agent", "my custom user agent");
            var response = new HttpResponseMessage();
            request.Properties[ScopeKeys.FunctionsHttpResponse] = response;

            // mock IP Address
            Mock<HttpContextBase> mockContext = new Mock<HttpContextBase>();
            Mock<HttpRequestBase> mockRequest = new Mock<HttpRequestBase>();
            mockRequest.Setup(r => r.UserHostAddress).Returns("1.2.3.4");
            mockContext.Setup(c => c.Request).Returns(mockRequest.Object);
            request.Properties[ScopeKeys.HttpContext] = mockContext.Object;

            ILogger logger = CreateLogger(LoggingCategories.Results);
            using (logger.BeginScope(new Dictionary<string, object> { [ScopeKeys.HttpRequest] = request }))
            {
                logger.LogFunctionResult(result);
            }

            RequestTelemetry telemetry = _channel.Telemetries.Single() as RequestTelemetry;

            Assert.Equal(_invocationId.ToString(), telemetry.Id);
            Assert.Equal(_invocationId.ToString(), telemetry.Context.Operation.Id);
            Assert.Equal(_functionName, telemetry.Name);
            Assert.Equal(_functionName, telemetry.Context.Operation.Name);
            Assert.Equal("1.2.3.4", telemetry.Context.Location.Ip);
            Assert.Equal("POST", telemetry.Properties[LoggingKeys.HttpMethod]);
            Assert.Equal(new Uri("http://someuri/api/path"), telemetry.Url);
            Assert.Equal("my custom user agent", telemetry.Context.User.UserAgent);
            // TODO: Beef up validation to include properties      
        }

        [Fact]
        public void Log_NoProperties_CreatesTraceAndCorrelates()
        {
            Guid scopeGuid = Guid.NewGuid();

            ILogger logger = CreateLogger(LoggingCategories.Function);
            using (logger.BeginScope(
                new Dictionary<string, object>
                {
                    [ScopeKeys.FunctionInvocationId] = scopeGuid,
                    [ScopeKeys.FunctionName] = "Test"
                }))
            {
                logger.LogInformation("Information");
                logger.LogCritical("Critical");
                logger.LogDebug("Debug");
                logger.LogError("Error");
                logger.LogTrace("Trace");
                logger.LogWarning("Warning");
            }

            Assert.Equal(6, _channel.Telemetries.Count);
            Assert.Equal(6, _channel.Telemetries.OfType<TraceTelemetry>().Count());
            foreach (var telemetry in _channel.Telemetries.Cast<TraceTelemetry>())
            {
                Microsoft.Extensions.Logging.LogLevel expectedLevel;
                Assert.True(Enum.TryParse(telemetry.Message, out expectedLevel));
                Microsoft.Extensions.Logging.LogLevel actualLevel;
                Assert.True(Enum.TryParse(telemetry.Properties[LoggingKeys.Level], out actualLevel));
                Assert.Equal(expectedLevel, actualLevel);

                Assert.Equal(LoggingCategories.Function, telemetry.Properties[LoggingKeys.CategoryName]);
                Assert.Equal(telemetry.Message, telemetry.Properties[LoggingKeys.CustomPropertyPrefix + LoggingKeys.OriginalFormat]);
                Assert.Equal(scopeGuid.ToString(), telemetry.Context.Operation.Id);
                Assert.Equal("Test", telemetry.Context.Operation.Name);
            }
        }

        [Fact]
        public void Log_WithProperties_IncludesProps()
        {
            ILogger logger = CreateLogger(LoggingCategories.Function);
            logger.LogInformation("Using {some} custom {properties}. {Test}.", "1", 2, "3");

            var telemetry = _channel.Telemetries.Single() as TraceTelemetry;

            Microsoft.Extensions.Logging.LogLevel actualLevel;
            Assert.True(Enum.TryParse(telemetry.Properties[LoggingKeys.Level], out actualLevel));
            Assert.Equal(LogLevel.Information, actualLevel);

            Assert.Equal(LoggingCategories.Function, telemetry.Properties[LoggingKeys.CategoryName]);
            Assert.Equal("Using {some} custom {properties}. {Test}.",
                telemetry.Properties[LoggingKeys.CustomPropertyPrefix + LoggingKeys.OriginalFormat]);
            Assert.Equal("Using 1 custom 2. 3.", telemetry.Message);
            Assert.Equal("1", telemetry.Properties[LoggingKeys.CustomPropertyPrefix + "some"]);
            Assert.Equal("2", telemetry.Properties[LoggingKeys.CustomPropertyPrefix + "properties"]);
            Assert.Equal("3", telemetry.Properties[LoggingKeys.CustomPropertyPrefix + "Test"]);
        }

        [Fact]
        public void Log_WithException_CreatesExceptionAndCorrelates()
        {
            var ex = new InvalidOperationException("Failure");
            Guid scopeGuid = Guid.NewGuid();
            ILogger logger = CreateLogger(LoggingCategories.Function);

            using (logger.BeginScope(
                new Dictionary<string, object>
                {
                    [ScopeKeys.FunctionInvocationId] = scopeGuid,
                    [ScopeKeys.FunctionName] = "Test"
                }))
            {
                logger.LogError(0, ex, "Error with customer: {customer}.", "John Doe");
            }

            var telemetry = _channel.Telemetries.Single() as ExceptionTelemetry;

            Microsoft.Extensions.Logging.LogLevel actualLevel;
            Assert.True(Enum.TryParse(telemetry.Properties[LoggingKeys.Level], out actualLevel));
            Assert.Equal(LogLevel.Error, actualLevel);

            Assert.Equal(LoggingCategories.Function, telemetry.Properties[LoggingKeys.CategoryName]);
            Assert.Equal("Error with customer: {customer}.",
                telemetry.Properties[LoggingKeys.CustomPropertyPrefix + LoggingKeys.OriginalFormat]);
            Assert.Equal("Error with customer: John Doe.", telemetry.Message);
            Assert.Equal("John Doe", telemetry.Properties[LoggingKeys.CustomPropertyPrefix + "customer"]);
            Assert.Same(ex, telemetry.Exception);
            Assert.Equal(scopeGuid.ToString(), telemetry.Context.Operation.Id);
            Assert.Equal("Test", telemetry.Context.Operation.Name);
        }

        [Fact]
        public async Task BeginScope()
        {
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < 20; i++)
            {
                tasks.Add(Level1(Guid.NewGuid()));
            }

            await Task.WhenAll(tasks);
        }

        private async Task Level1(Guid asyncLocalSetting)
        {
            // Push and pop values onto the dictionary at various levels. Make sure they
            // maintain their AsyncLocal state
            var level1 = new Dictionary<string, object>
            {
                ["AsyncLocal"] = asyncLocalSetting,
                ["1"] = 1
            };

            ILogger logger = CreateLogger(LoggingCategories.Function);
            using (logger.BeginScope(level1))
            {
                ValidateScope(level1);

                await Level2(asyncLocalSetting);

                ValidateScope(level1);
            }
        }

        private async Task Level2(Guid asyncLocalSetting)
        {
            await Task.Delay(1);

            var level2 = new Dictionary<string, object>
            {
                ["2"] = 2
            };

            var expectedLevel2 = new Dictionary<string, object>
            {
                ["1"] = 1,
                ["2"] = 2,
                ["AsyncLocal"] = asyncLocalSetting
            };

            ILogger logger2 = CreateLogger(LoggingCategories.Function);
            using (logger2.BeginScope(level2))
            {
                ValidateScope(expectedLevel2);

                await Level3(asyncLocalSetting);

                ValidateScope(expectedLevel2);
            }
        }

        private async Task Level3(Guid asyncLocalSetting)
        {
            await Task.Delay(1);

            // also overwrite value 1, we expect this to win here
            var level3 = new Dictionary<string, object>
            {
                ["1"] = 11,
                ["3"] = 3
            };

            var expectedLevel3 = new Dictionary<string, object>
            {
                ["1"] = 11,
                ["2"] = 2,
                ["3"] = 3,
                ["AsyncLocal"] = asyncLocalSetting
            };

            ILogger logger3 = CreateLogger(LoggingCategories.Function);
            using (logger3.BeginScope(level3))
            {
                ValidateScope(expectedLevel3);
            }
        }

        private ILogger CreateLogger(string category)
        {
            return new AppInsightsLogger(_client, category, null);
        }

        private static void ValidateScope(IDictionary<string, object> expected)
        {
            var scopeDict = AppInsightsScope.Current.GetMergedStateDictionary();
            Assert.Equal(expected.Count, scopeDict.Count);
            foreach (var entry in expected)
            {
                Assert.Equal(entry.Value, scopeDict[entry.Key]);
            }
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

    }
}
