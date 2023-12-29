// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.SnapshotCollector;
using Microsoft.ApplicationInsights.WindowsServer.Channel.Implementation;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Loggers
{
    public class ApplicationInsightsLoggerTests : IDisposable
    {
        private readonly Guid _invocationId = Guid.NewGuid();
        private readonly Guid _hostInstanceId = Guid.NewGuid();
        private readonly DateTime _startTime = DateTime.UtcNow;
        private readonly DateTime _endTime;
        private readonly string _triggerReason = "new queue message";
        private readonly string _functionFullName = "Functions.TestFunction";
        private readonly string _functionShortName = "TestFunction";
        private readonly string _functionCategoryName;
        private readonly IDictionary<string, string> _arguments;
        private readonly TestTelemetryChannel _channel = new TestTelemetryChannel();
        private readonly string defaultIp = "0.0.0.0";
        private readonly TelemetryClient _client;
        private readonly int _durationMs = 450;
        private readonly IHost _host;

        public ApplicationInsightsLoggerTests()
        {
            _functionCategoryName = LogCategories.CreateFunctionUserCategory(_functionShortName);
            _endTime = _startTime.AddMilliseconds(_durationMs);
            _arguments = new Dictionary<string, string>
            {
                ["queueMessage"] = "my message",
                ["anotherParam"] = "some value"
            };

            _host = new HostBuilder()
                .ConfigureLogging(b =>
                {
                    b.SetMinimumLevel(LogLevel.Trace);
                    b.AddApplicationInsightsWebJobs(o =>
                    {
                        o.InstrumentationKey = "some key";
                    });
                }).Build();

            TelemetryConfiguration telemteryConfiguration = _host.Services.GetService<TelemetryConfiguration>();
            telemteryConfiguration.TelemetryChannel = _channel;

            _client = _host.Services.GetService<TelemetryClient>();

            var descriptor = new FunctionDescriptor
            {
                FullName = _functionFullName,
                ShortName = _functionShortName
            };
        }

        [Fact]
        public async Task LogFunctionResult_Succeeded_SendsCorrectTelemetry()
        {
            var result = CreateDefaultInstanceLogEntry();
            ILogger logger = CreateLogger(LogCategories.Results);

            string expectedOperationId, expectedRequestId;
            using (logger.BeginFunctionScope(CreateFunctionInstance(_invocationId), _hostInstanceId))
            {
                expectedRequestId = Activity.Current.SpanId.ToHexString();
                expectedOperationId = Activity.Current.TraceId.ToHexString();

                // sleep briefly to provide a non-zero Duration
                await Task.Delay(100);
                logger.LogFunctionResult(result);
            }

            RequestTelemetry telemetry = _channel.Telemetries.Single() as RequestTelemetry;

            Assert.Equal(expectedRequestId, telemetry.Id);
            Assert.Equal(expectedOperationId, telemetry.Context.Operation.Id);
            Assert.Null(telemetry.Context.Operation.ParentId);
            Assert.Contains(LogConstants.InvocationIdKey, telemetry.Properties.Keys);
            Assert.Contains(LogConstants.ProcessIdKey, telemetry.Properties.Keys);
            Assert.Equal(_invocationId.ToString(), telemetry.Properties[LogConstants.InvocationIdKey]);
            Assert.Equal(_functionShortName, telemetry.Name);
            Assert.Equal(_functionShortName, telemetry.Context.Operation.Name);
            Assert.True(telemetry.Duration > TimeSpan.Zero, "Expected a non-zero Duration.");
            Assert.Equal(defaultIp, telemetry.Context.Location.Ip);
            Assert.Equal(LogCategories.Results, telemetry.Properties[LogConstants.CategoryNameKey]);
            Assert.Equal(LogLevel.Information.ToString(), telemetry.Properties[LogConstants.LogLevelKey]);
            Assert.Equal(_triggerReason, telemetry.Properties[LogConstants.TriggerReasonKey]);
            // TODO: Beef up validation to include properties
        }

        [Fact]
        public void LogFunctionResult_Failed_SendsCorrectTelemetry()
        {
            FunctionInvocationException fex = new FunctionInvocationException("Failed");
            var result = CreateDefaultInstanceLogEntry(fex);
            ILogger logger = CreateLogger(LogCategories.Results);

            string expectedOperationId, expectedRequestId;
            using (logger.BeginFunctionScope(CreateFunctionInstance(_invocationId), _hostInstanceId))
            {
                expectedRequestId = Activity.Current.SpanId.ToHexString();
                expectedOperationId = Activity.Current.TraceId.ToHexString();

                logger.LogFunctionResult(result);
            }

            // Errors log an associated Exception
            RequestTelemetry requestTelemetry = _channel.Telemetries.OfType<RequestTelemetry>().Single();
            ExceptionTelemetry exceptionTelemetry = _channel.Telemetries.OfType<ExceptionTelemetry>().Single();

            Assert.Equal(2, _channel.Telemetries.Count);
            Assert.Equal(expectedRequestId, requestTelemetry.Id);
            Assert.Equal(expectedOperationId, requestTelemetry.Context.Operation.Id);
            Assert.Null(requestTelemetry.Context.Operation.ParentId);
            Assert.Equal(_functionShortName, requestTelemetry.Name);
            Assert.Equal(_functionShortName, requestTelemetry.Context.Operation.Name);
            Assert.Equal(defaultIp, requestTelemetry.Context.Location.Ip);
            Assert.Contains(LogConstants.InvocationIdKey, requestTelemetry.Properties.Keys);
            Assert.Contains(LogConstants.ProcessIdKey, requestTelemetry.Properties.Keys);
            Assert.Equal(_invocationId.ToString(), requestTelemetry.Properties[LogConstants.InvocationIdKey]);
            Assert.Equal(LogCategories.Results, requestTelemetry.Properties[LogConstants.CategoryNameKey]);
            Assert.Equal(LogLevel.Error.ToString(), requestTelemetry.Properties[LogConstants.LogLevelKey]);
            // TODO: Beef up validation to include properties

            // Exception needs to have associated id
            Assert.Equal(expectedOperationId, exceptionTelemetry.Context.Operation.Id);
            Assert.Equal(expectedRequestId, exceptionTelemetry.Context.Operation.ParentId);
            Assert.Equal(_functionShortName, exceptionTelemetry.Context.Operation.Name);
            Assert.Same(fex, exceptionTelemetry.Exception);
            Assert.Equal(LogCategories.Results, exceptionTelemetry.Properties[LogConstants.CategoryNameKey]);
            Assert.Equal(LogLevel.Error.ToString(), exceptionTelemetry.Properties[LogConstants.LogLevelKey]);
            // TODO: Beef up validation to include properties
        }

        [Fact]
        public void LogFunctionAggregate_SendsCorrectTelemetry()
        {
            DateTime now = DateTime.UtcNow;
            var resultAggregate = new FunctionResultAggregate
            {
                Name = _functionFullName,
                Failures = 4,
                Successes = 116,
                MinDuration = TimeSpan.FromMilliseconds(200),
                MaxDuration = TimeSpan.FromMilliseconds(2180),
                AverageDuration = TimeSpan.FromMilliseconds(340),
                Timestamp = now
            };

            ILogger logger = CreateLogger(LogCategories.Aggregator);
            logger.LogFunctionResultAggregate(resultAggregate);

            IEnumerable<MetricTelemetry> metrics = _channel.Telemetries.Cast<MetricTelemetry>();
            // turn them into a dictionary so we can easily validate
            IDictionary<string, MetricTelemetry> metricDict = metrics.ToDictionary(m => m.Name, m => m);

            Assert.Equal(7, metricDict.Count);

            ValidateMetric(metricDict[$"{_functionFullName} {LogConstants.FailuresKey}"], 4, LogLevel.Information);
            ValidateMetric(metricDict[$"{_functionFullName} {LogConstants.SuccessesKey}"], 116, LogLevel.Information);
            ValidateMetric(metricDict[$"{_functionFullName} {LogConstants.MinDurationKey}"], 200, LogLevel.Information);
            ValidateMetric(metricDict[$"{_functionFullName} {LogConstants.MaxDurationKey}"], 2180, LogLevel.Information);
            ValidateMetric(metricDict[$"{_functionFullName} {LogConstants.AverageDurationKey}"], 340, LogLevel.Information);
            ValidateMetric(metricDict[$"{_functionFullName} {LogConstants.SuccessRateKey}"], 96.67, LogLevel.Information);
            ValidateMetric(metricDict[$"{_functionFullName} {LogConstants.CountKey}"], 120, LogLevel.Information);
        }

        private static void ValidateMetric(MetricTelemetry metric, double expectedValue, LogLevel expectedLevel, string expectedCategory = LogCategories.Aggregator)
        {
            Assert.Equal(expectedValue, metric.Value);
            Assert.Equal(3, metric.Properties.Count);
            Assert.Equal(expectedCategory, metric.Properties[LogConstants.CategoryNameKey]);
            Assert.Equal(expectedLevel.ToString(), metric.Properties[LogConstants.LogLevelKey]);
            Assert.Equal(Process.GetCurrentProcess().Id.ToString(), metric.Properties[LogConstants.ProcessIdKey]);
        }

        [Fact]
        public void Log_NoProperties_CreatesTraceAndCorrelates()
        {
            Guid scopeGuid = Guid.NewGuid();

            ILogger logger = CreateLogger(_functionCategoryName);

            string expectedOperationId, expectedRequestId;
            using (logger.BeginFunctionScope(CreateFunctionInstance(scopeGuid), _hostInstanceId))
            {
                logger.LogInformation("Information");
                logger.LogCritical("Critical");
                logger.LogDebug("Debug");
                logger.LogError("Error");
                logger.LogTrace("Trace");
                logger.LogWarning("Warning");

                expectedRequestId = Activity.Current.SpanId.ToHexString();
                expectedOperationId = Activity.Current.TraceId.ToHexString();
            }

            Assert.Equal(6, _channel.Telemetries.Count);
            Assert.Equal(6, _channel.Telemetries.OfType<TraceTelemetry>().Count());
            foreach (var telemetry in _channel.Telemetries.Cast<TraceTelemetry>())
            {
                Enum.TryParse(telemetry.Message, out LogLevel expectedLogLevel);
                Assert.Equal(expectedLogLevel.ToString(), telemetry.Properties[LogConstants.LogLevelKey]);

                SeverityLevel expectedSeverityLevel;
                if (telemetry.Message == "Trace" || telemetry.Message == "Debug")
                {
                    expectedSeverityLevel = SeverityLevel.Verbose;
                }
                else
                {
                    Assert.True(Enum.TryParse(telemetry.Message, out expectedSeverityLevel));
                }
                Assert.Equal(expectedSeverityLevel, telemetry.SeverityLevel);

                Assert.Equal(5, telemetry.Properties.Count);
                Assert.Equal(Process.GetCurrentProcess().Id.ToString(), telemetry.Properties[LogConstants.ProcessIdKey]);
                Assert.Equal(_functionCategoryName, telemetry.Properties[LogConstants.CategoryNameKey]);
                Assert.Equal(telemetry.Message, telemetry.Properties[LogConstants.CustomPropertyPrefix + LogConstants.OriginalFormatKey]);
                Assert.Equal(scopeGuid.ToString(), telemetry.Properties[LogConstants.InvocationIdKey]);
                Assert.Equal(telemetry.Message, telemetry.Properties[LogConstants.LogLevelKey]);

                Assert.Equal(expectedRequestId, telemetry.Context.Operation.ParentId);
                Assert.Equal(expectedOperationId, telemetry.Context.Operation.Id);
                Assert.Equal(_functionShortName, telemetry.Context.Operation.Name);
            }
        }

        [Fact]
        public void Log_WithProperties_IncludesProps()
        {
            ILogger logger = CreateLogger(_functionCategoryName);
            logger.LogInformation("Using {some} custom {properties}. {Test}.", "1", 2, "3");

            var telemetry = _channel.Telemetries.Single() as TraceTelemetry;

            Assert.Equal(SeverityLevel.Information, telemetry.SeverityLevel);

            Assert.Equal(_functionCategoryName, telemetry.Properties[LogConstants.CategoryNameKey]);
            Assert.Equal(LogLevel.Information.ToString(), telemetry.Properties[LogConstants.LogLevelKey]);
            Assert.Equal("Using {some} custom {properties}. {Test}.",
                telemetry.Properties[LogConstants.CustomPropertyPrefix + LogConstants.OriginalFormatKey]);
            Assert.Equal("Using 1 custom 2. 3.", telemetry.Message);
            Assert.Equal("1", telemetry.Properties[LogConstants.CustomPropertyPrefix + "some"]);
            Assert.Equal("2", telemetry.Properties[LogConstants.CustomPropertyPrefix + "properties"]);
            Assert.Equal("3", telemetry.Properties[LogConstants.CustomPropertyPrefix + "Test"]);
        }

        [Fact]
        public void Log_WithException_CreatesExceptionAndCorrelates()
        {
            var ex = new InvalidOperationException("Failure");
            Guid scopeGuid = Guid.NewGuid();
            ILogger logger = CreateLogger(_functionCategoryName);

            string expectedOperationId, expectedRequestId;

            using (logger.BeginFunctionScope(CreateFunctionInstance(scopeGuid), _hostInstanceId))
            {
                logger.LogError(0, ex, "Error with customer: {customer}.", "John Doe");

                expectedRequestId = Activity.Current.SpanId.ToHexString();
                expectedOperationId = Activity.Current.TraceId.ToHexString();
            }

            void ValidateProperties(ISupportProperties telemetry)
            {
                Assert.Equal(scopeGuid.ToString(), telemetry.Properties[LogConstants.InvocationIdKey]);
                Assert.Equal(_functionCategoryName, telemetry.Properties[LogConstants.CategoryNameKey]);
                Assert.Equal(LogLevel.Error.ToString(), telemetry.Properties[LogConstants.LogLevelKey]);
                Assert.Equal("Error with customer: {customer}.", telemetry.Properties[LogConstants.CustomPropertyPrefix + LogConstants.OriginalFormatKey]);
                Assert.Equal("John Doe", telemetry.Properties[LogConstants.CustomPropertyPrefix + "customer"]);
            }

            Assert.Equal(2, _channel.Telemetries.Count());
            var exceptionTelemetry = _channel.Telemetries.OfType<ExceptionTelemetry>().Single();
            var traceTelemetry = _channel.Telemetries.OfType<TraceTelemetry>().Single();

            Assert.Equal(SeverityLevel.Error, exceptionTelemetry.SeverityLevel);

            // verify the properties for the telemetries
            Assert.Equal(7, exceptionTelemetry.Properties.Count);
            Assert.Equal(6, traceTelemetry.Properties.Count);
            ValidateProperties(exceptionTelemetry);
            ValidateProperties(traceTelemetry);
            // We add the formatted message as a property for exceptions
            Assert.Equal("Error with customer: John Doe.", exceptionTelemetry.Properties[LogConstants.FormattedMessageKey]);

            Assert.Same(ex, exceptionTelemetry.Exception);
            Assert.Equal(Process.GetCurrentProcess().Id.ToString(), exceptionTelemetry.Properties[LogConstants.ProcessIdKey]);
            Assert.Equal(expectedOperationId, exceptionTelemetry.Context.Operation.Id);
            Assert.Equal(expectedRequestId, exceptionTelemetry.Context.Operation.ParentId);
            Assert.Equal(_functionShortName, exceptionTelemetry.Context.Operation.Name);

            string internalMessage = GetInternalExceptionMessages(exceptionTelemetry).Single();
            Assert.Equal("Failure", internalMessage);

            // We should not have the request logged.
            Assert.False(exceptionTelemetry.Properties.TryGetValue(LogConstants.CustomPropertyPrefix + ApplicationInsightsScopeKeys.HttpRequest, out string request));
        }

        [Fact]
        public void LogMetric_NoProperties()
        {
            ILogger logger = CreateLogger(_functionCategoryName);
            Guid scopeGuid = Guid.NewGuid();

            string expectedOperationId, expectedRequestId;
            using (logger.BeginFunctionScope(CreateFunctionInstance(scopeGuid), _hostInstanceId))
            {
                logger.LogMetric("CustomMetric", 44.9);

                expectedRequestId = Activity.Current.SpanId.ToHexString();
                expectedOperationId = Activity.Current.TraceId.ToHexString();
            }

            var telemetry = _channel.Telemetries.Single() as MetricTelemetry;

            Assert.Equal(5, telemetry.Properties.Count);
            Assert.Equal(Process.GetCurrentProcess().Id.ToString(), telemetry.Properties[LogConstants.ProcessIdKey]);
            Assert.Equal(scopeGuid.ToString(), telemetry.Properties[LogConstants.InvocationIdKey]);
            Assert.Equal(_functionCategoryName, telemetry.Properties[LogConstants.CategoryNameKey]);
            Assert.Equal(LogLevel.Information.ToString(), telemetry.Properties[LogConstants.LogLevelKey]);

            // metrics are logged with EventId=1
            Assert.Equal("1", telemetry.Properties[LogConstants.EventIdKey]);

            Assert.Equal("CustomMetric", telemetry.Name);
            Assert.Equal(44.9, telemetry.Sum);

            Assert.Equal(expectedOperationId, telemetry.Context.Operation.Id);
            Assert.Equal(expectedRequestId, telemetry.Context.Operation.ParentId);
            Assert.Equal(_functionShortName, telemetry.Context.Operation.Name);

            Assert.Null(telemetry.Min);
            Assert.Null(telemetry.Max);
            Assert.Equal(1, telemetry.Count);
            Assert.Null(telemetry.StandardDeviation);
        }

        [Fact]
        public void Log_IncludesEventId()
        {
            ILogger logger = CreateLogger(_functionCategoryName);
            logger.Log(LogLevel.Information, new EventId(100, "TestEvent"), "Test", null, (s, e) => s);

            var telemetry = _channel.Telemetries.Single() as ISupportProperties;

            Assert.Equal(5, telemetry.Properties.Count);
            Assert.Equal(Process.GetCurrentProcess().Id.ToString(), telemetry.Properties[LogConstants.ProcessIdKey]);
            Assert.Equal(_functionCategoryName, telemetry.Properties[LogConstants.CategoryNameKey]);
            Assert.Equal(LogLevel.Information.ToString(), telemetry.Properties[LogConstants.LogLevelKey]);
            Assert.Equal("100", telemetry.Properties[LogConstants.EventIdKey]);
            Assert.Equal("TestEvent", telemetry.Properties[LogConstants.EventNameKey]);
        }

        [Fact]
        public void Log_IgnoresEventIdZero()
        {
            ILogger logger = CreateLogger(_functionCategoryName);
            logger.Log(LogLevel.Information, 0, "Test", null, (s, e) => s);

            var telemetry = _channel.Telemetries.Single() as ISupportProperties;

            Assert.Equal(3, telemetry.Properties.Count);
            Assert.Equal(Process.GetCurrentProcess().Id.ToString(), telemetry.Properties[LogConstants.ProcessIdKey]);
            Assert.Equal(_functionCategoryName, telemetry.Properties[LogConstants.CategoryNameKey]);
            Assert.Equal(LogLevel.Information.ToString(), telemetry.Properties[LogConstants.LogLevelKey]);
        }

        [Fact]
        public void LogMetric_AllProperties()
        {
            ILogger logger = CreateLogger(_functionCategoryName);
            Guid scopeGuid = Guid.NewGuid();

            string expectedOperationId, expectedRequestId;
            using (logger.BeginFunctionScope(CreateFunctionInstance(scopeGuid), _hostInstanceId))
            {
                expectedRequestId = Activity.Current.SpanId.ToHexString();
                expectedOperationId = Activity.Current.TraceId.ToHexString();

                var props = new Dictionary<string, object>
                {
                    ["MyCustomProp1"] = "abc",
                    ["MyCustomProp2"] = "def",
                    ["Count"] = 2,
                    ["Min"] = 3.3,
                    ["Max"] = 4.4,
                    ["StandardDeviation"] = 5.5
                };
                logger.LogMetric("CustomMetric", 1.1, props);
            }

            var telemetry = _channel.Telemetries.Single() as MetricTelemetry;

            Assert.Equal(7, telemetry.Properties.Count);
            Assert.Equal(Process.GetCurrentProcess().Id.ToString(), telemetry.Properties[LogConstants.ProcessIdKey]);
            Assert.Equal(scopeGuid.ToString(), telemetry.Properties[LogConstants.InvocationIdKey]);
            Assert.Equal(_functionCategoryName, telemetry.Properties[LogConstants.CategoryNameKey]);
            Assert.Equal(LogLevel.Information.ToString(), telemetry.Properties[LogConstants.LogLevelKey]);
            Assert.Equal("abc", telemetry.Properties[$"{LogConstants.CustomPropertyPrefix}MyCustomProp1"]);
            Assert.Equal("def", telemetry.Properties[$"{LogConstants.CustomPropertyPrefix}MyCustomProp2"]);

            // metrics are logged with EventId=1
            Assert.Equal("1", telemetry.Properties[LogConstants.EventIdKey]);

            Assert.Equal(expectedOperationId, telemetry.Context.Operation.Id);
            Assert.Equal(expectedRequestId, telemetry.Context.Operation.ParentId);
            Assert.Equal(_functionShortName, telemetry.Context.Operation.Name);

            Assert.Equal("CustomMetric", telemetry.Name);
            Assert.Equal(1.1, telemetry.Sum);
            Assert.Equal(2, telemetry.Count);
            Assert.Equal(3.3, telemetry.Min);
            Assert.Equal(4.4, telemetry.Max);
            Assert.Equal(5.5, telemetry.StandardDeviation);
        }

        [Fact]
        public void LogMetric_AllProperties_Lowercase()
        {
            ILogger logger = CreateLogger(_functionCategoryName);
            Guid scopeGuid = Guid.NewGuid();

            string expectedOperationId, expectedRequestId;
            using (logger.BeginFunctionScope(CreateFunctionInstance(scopeGuid), _hostInstanceId))
            {
                var props = new Dictionary<string, object>
                {
                    ["MyCustomProp1"] = "abc",
                    ["MyCustomProp2"] = "def",
                    ["count"] = 2,
                    ["min"] = 3.3,
                    ["max"] = 4.4,
                    ["standardDeviation"] = 5.5
                };
                logger.LogMetric("CustomMetric", 1.1, props);

                expectedRequestId = Activity.Current.SpanId.ToHexString();
                expectedOperationId = Activity.Current.TraceId.ToHexString();
            }

            var telemetry = _channel.Telemetries.Single() as MetricTelemetry;

            Assert.Equal(7, telemetry.Properties.Count);
            Assert.Equal(Process.GetCurrentProcess().Id.ToString(), telemetry.Properties[LogConstants.ProcessIdKey]);
            Assert.Equal(scopeGuid.ToString(), telemetry.Properties[LogConstants.InvocationIdKey]);
            Assert.Equal(_functionCategoryName, telemetry.Properties[LogConstants.CategoryNameKey]);
            Assert.Equal(LogLevel.Information.ToString(), telemetry.Properties[LogConstants.LogLevelKey]);
            Assert.Equal("abc", telemetry.Properties[$"{LogConstants.CustomPropertyPrefix}MyCustomProp1"]);
            Assert.Equal("def", telemetry.Properties[$"{LogConstants.CustomPropertyPrefix}MyCustomProp2"]);

            // metrics are logged with EventId=1
            Assert.Equal("1", telemetry.Properties[LogConstants.EventIdKey]);

            Assert.Equal(expectedOperationId, telemetry.Context.Operation.Id);
            Assert.Equal(expectedRequestId, telemetry.Context.Operation.ParentId);
            Assert.Equal(_functionShortName, telemetry.Context.Operation.Name);

            Assert.Equal("CustomMetric", telemetry.Name);
            Assert.Equal(1.1, telemetry.Sum);
            Assert.Equal(2, telemetry.Count);
            Assert.Equal(3.3, telemetry.Min);
            Assert.Equal(4.4, telemetry.Max);
            Assert.Equal(5.5, telemetry.StandardDeviation);
        }

        [Theory]
        [InlineData("1.2.3.4:5")]
        [InlineData("1.2.3.4")]
        public void GetIpAddress_ChecksHeaderFirst(string headerIp)
        {
            var request = new Mock<HttpRequest>();
            var headers = new HeaderDictionary
            {
                { ApplicationInsightsScopeKeys.ForwardedForHeaderName, headerIp }
            };
            request.SetupGet(r => r.Headers).Returns(headers);

            MockHttpRequest(request, "5.6.7.8");

            string ip = ApplicationInsightsLogger.GetIpAddress(request.Object);

            Assert.Equal("1.2.3.4", ip);
        }

        [Fact]
        public void GetIpAddress_ChecksContextSecond()
        {
            var request = new Mock<HttpRequest>();

            MockHttpRequest(request, "5.6.7.8");

            string ip = ApplicationInsightsLogger.GetIpAddress(request.Object);

            Assert.Equal("5.6.7.8", ip);
        }

        [Fact]
        public void Log_AcceptsStringsAsState()
        {
            var logger = CreateLogger(_functionCategoryName);
            logger.Log(LogLevel.Information, 0, "some string", null, (s, e) => s.ToString());

            var telemetry = _channel.Telemetries.Single() as TraceTelemetry;
            Assert.Equal("some string", telemetry.Message);
            Assert.Equal(_functionCategoryName, telemetry.Properties[LogConstants.CategoryNameKey]);
        }

        [Fact]
        public void Log_Exception_NoLogMessage()
        {
            var logger = CreateLogger(_functionCategoryName);
            var innerEx = new Exception("Inner");
            var outerEx = new Exception("Outer", innerEx);

            logger.LogError(0, outerEx, string.Empty);

            var telemetry = _channel.Telemetries.Single() as ExceptionTelemetry;

            string[] internalMessages = GetInternalExceptionMessages(telemetry).ToArray();

            Assert.Equal(2, internalMessages.Length);
            Assert.Equal("Outer", internalMessages[0]);
            Assert.Equal("Inner", internalMessages[1]);

            Assert.Equal(outerEx, telemetry.Exception);
            Assert.Equal(_functionCategoryName, telemetry.Properties[LogConstants.CategoryNameKey]);
            Assert.Equal(LogLevel.Error.ToString(), telemetry.Properties[LogConstants.LogLevelKey]);
            Assert.DoesNotContain(LogConstants.FormattedMessageKey, telemetry.Properties.Keys);
        }

        [Fact]
        public void Log_Exception_LogMessage()
        {
            var logger = CreateLogger(_functionCategoryName);
            var innerEx = new Exception("Inner");
            var outerEx = new Exception("Outer", innerEx);

            logger.LogError(0, outerEx, "Log message");

            Assert.Equal(2, _channel.Telemetries.Count());
            var exceptionTelemetry = _channel.Telemetries.OfType<ExceptionTelemetry>().Single();
            var traceTelemetry = _channel.Telemetries.OfType<TraceTelemetry>().Single();

            string[] internalMessages = GetInternalExceptionMessages(exceptionTelemetry).ToArray();

            Assert.Equal(2, internalMessages.Length);
            Assert.Equal("Outer", internalMessages[0]);
            Assert.Equal("Inner", internalMessages[1]);

            Assert.Equal(outerEx, exceptionTelemetry.Exception);
            Assert.Equal(_functionCategoryName, exceptionTelemetry.Properties[LogConstants.CategoryNameKey]);
            Assert.Equal(LogLevel.Error.ToString(), exceptionTelemetry.Properties[LogConstants.LogLevelKey]);
            Assert.Equal("Log message", exceptionTelemetry.Properties[LogConstants.FormattedMessageKey]);
        }

        [Fact]
        public void Log_LogLevelNone_Succeeds()
        {
            Guid scopeGuid = Guid.NewGuid();

            ILogger logger = CreateLogger(_functionCategoryName);

            using (logger.BeginFunctionScope(CreateFunctionInstance(scopeGuid), _hostInstanceId))
            {
                logger.Log(LogLevel.None, "None");
            }

            Assert.Single(_channel.Telemetries);
            Assert.Single(_channel.Telemetries.OfType<TraceTelemetry>());
            foreach (var telemetry in _channel.Telemetries.Cast<TraceTelemetry>())
            {
                Enum.TryParse(telemetry.Message, out LogLevel expectedLogLevel);
                Assert.Equal(expectedLogLevel.ToString(), telemetry.Properties[LogConstants.LogLevelKey]);
                Assert.Null(telemetry.SeverityLevel);

                Assert.Equal(5, telemetry.Properties.Count);
                Assert.Equal(Process.GetCurrentProcess().Id.ToString(), telemetry.Properties[LogConstants.ProcessIdKey]);
                Assert.Equal(_functionCategoryName, telemetry.Properties[LogConstants.CategoryNameKey]);
                Assert.Equal(telemetry.Message, telemetry.Properties[LogConstants.CustomPropertyPrefix + LogConstants.OriginalFormatKey]);
                Assert.Equal(scopeGuid.ToString(), telemetry.Properties[LogConstants.InvocationIdKey]);
                Assert.Equal(telemetry.Message, telemetry.Properties[LogConstants.LogLevelKey]);
            }
        }

        [Fact]
        public void DuplicateProperties_LastStateWins()
        {
            var logger = CreateLogger(_functionCategoryName);

            var scope = new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("Prop", "1"),
                new KeyValuePair<string, object>("Prop", "2")
            };

            var state = new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("Prop", "3"),
                new KeyValuePair<string, object>("Prop", "4")
            };

            // LogMetric only takes dictionary
            var dict = new Dictionary<string, object>
            {
                { "Prop", "5" }
            };

            using (logger.BeginScope(scope))
            {
                logger.Log(LogLevel.Information, 0, state, null, (s, e) => string.Empty);
                logger.LogMetric("Test", 0, dict);
            }

            var trace = _channel.Telemetries.OfType<TraceTelemetry>().Single();
            Assert.Equal("4", trace.Properties[$"{LogConstants.CustomPropertyPrefix}Prop"]);

            var metric = _channel.Telemetries.OfType<MetricTelemetry>().Single();
            Assert.Equal("5", metric.Properties[$"{LogConstants.CustomPropertyPrefix}Prop"]);
        }

        private static IEnumerable<string> GetInternalExceptionMessages(ExceptionTelemetry telemetry)
        {
            IList<string> internalMessages = new List<string>();

            // The transmitted details may get out-of-sync with the Exception. We previously had bugs 
            // around this, so double-checking that the exception messages remain as intended. These are 
            // all internal to App Insights so pull them out with reflection.
            PropertyInfo exceptionsProp = typeof(ExceptionTelemetry).GetProperty("Exceptions", BindingFlags.NonPublic | BindingFlags.Instance);
            var details = exceptionsProp.GetValue(telemetry) as IEnumerable<object>;

            foreach (var detail in details)
            {
                var messageProp = detail.GetType().GetProperty("message", BindingFlags.Public | BindingFlags.Instance);
                internalMessages.Add(messageProp.GetValue(detail) as string);
            }

            return internalMessages;
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

        [Fact]
        public void ApplicationInsightsLoggerOptions_Format()
        {
            var options = new ApplicationInsightsLoggerOptions
            {
                EnableLiveMetrics = true,
                EnableLiveMetricsFilters = true,
                SamplingSettings = new SamplingPercentageEstimatorSettings()
                {
                    EvaluationInterval = TimeSpan.FromHours(1),
                    InitialSamplingPercentage = 0.1,
                    MaxSamplingPercentage = 0.1,
                    MaxTelemetryItemsPerSecond = 42,
                    MinSamplingPercentage = 0.1,
                    MovingAverageRatio = 1.0,
                    SamplingPercentageDecreaseTimeout = TimeSpan.FromSeconds(1),
                    SamplingPercentageIncreaseTimeout = TimeSpan.FromSeconds(1)
                },
                SnapshotConfiguration = new SnapshotCollectorConfiguration()
                {
                    AgentEndpoint = "http://something",
                    FailedRequestLimit = 42,
                    IsEnabled = false,
                    IsEnabledInDeveloperMode = false,
                    IsEnabledWhenProfiling = false,
                    IsLowPrioritySnapshotUploader = false,
                    MaximumCollectionPlanSize = 42,
                    MaximumSnapshotsRequired = 42,
                    ProblemCounterResetInterval = TimeSpan.FromMinutes(42),
                    ProvideAnonymousTelemetry = false,
                    ReconnectInterval = TimeSpan.FromMinutes(42),
                    ShadowCopyFolder = "123",
                    SnapshotInLowPriorityThread = false,
                    SnapshotsPerDayLimit = 42,
                    SnapshotsPerTenMinutesLimit = 42,
                    TempFolder = "123",
                    ThresholdForSnapshotting = 42,
                    UploaderProxy = "123"
                },
                HttpAutoCollectionOptions = new HttpAutoCollectionOptions()
                {
                    EnableResponseHeaderInjection = false,
                    EnableW3CDistributedTracing = false,
                    EnableHttpTriggerExtendedInfoCollection = true
                },
                DependencyTrackingOptions = new DependencyTrackingOptions()
                {
                    DisableDiagnosticSourceInstrumentation = true,
                    DisableRuntimeInstrumentation = true,
                    EnableSqlCommandTextInstrumentation = true,
                    EnableAzureSdkTelemetryListener = true,
                    EnableLegacyCorrelationHeadersInjection = true,
                    EnableRequestIdHeaderInjectionInW3CMode = true,
                    SetComponentCorrelationHttpHeaders = true
                },                
                TokenCredentialOptions = TokenCredentialOptions.ParseAuthenticationString($"Authorization=AAD;ClientId={Guid.NewGuid()}")
            };

            var deserializedOptions = JsonConvert.DeserializeObject<ApplicationInsightsLoggerOptions>(options.Format());

            Assert.Equal(options.HttpAutoCollectionOptions.EnableHttpTriggerExtendedInfoCollection, deserializedOptions.HttpAutoCollectionOptions.EnableHttpTriggerExtendedInfoCollection);
            Assert.Equal(options.HttpAutoCollectionOptions.EnableW3CDistributedTracing, deserializedOptions.HttpAutoCollectionOptions.EnableW3CDistributedTracing);
            Assert.Equal(options.HttpAutoCollectionOptions.EnableResponseHeaderInjection, deserializedOptions.HttpAutoCollectionOptions.EnableResponseHeaderInjection);
            Assert.Equal(options.SamplingSettings.EvaluationInterval, deserializedOptions.SamplingSettings.EvaluationInterval);
            Assert.Equal(options.SamplingSettings.InitialSamplingPercentage, deserializedOptions.SamplingSettings.InitialSamplingPercentage);
            Assert.Equal(options.SamplingSettings.MaxSamplingPercentage, deserializedOptions.SamplingSettings.MaxSamplingPercentage);
            Assert.Equal(options.SamplingSettings.MaxTelemetryItemsPerSecond, deserializedOptions.SamplingSettings.MaxTelemetryItemsPerSecond);
            Assert.Equal(options.SamplingSettings.MinSamplingPercentage, deserializedOptions.SamplingSettings.MinSamplingPercentage);
            Assert.Equal(options.SamplingSettings.MovingAverageRatio, deserializedOptions.SamplingSettings.MovingAverageRatio);
            Assert.Equal(options.SamplingSettings.SamplingPercentageDecreaseTimeout, deserializedOptions.SamplingSettings.SamplingPercentageDecreaseTimeout);
            Assert.Equal(options.SamplingSettings.SamplingPercentageIncreaseTimeout, deserializedOptions.SamplingSettings.SamplingPercentageIncreaseTimeout);

            Assert.Equal(options.SnapshotConfiguration.FailedRequestLimit, deserializedOptions.SnapshotConfiguration.FailedRequestLimit);
            Assert.Equal(options.SnapshotConfiguration.IsEnabled, deserializedOptions.SnapshotConfiguration.IsEnabled);
            Assert.Equal(options.SnapshotConfiguration.IsEnabledInDeveloperMode, deserializedOptions.SnapshotConfiguration.IsEnabledInDeveloperMode);
            Assert.Equal(options.SnapshotConfiguration.IsEnabledWhenProfiling, deserializedOptions.SnapshotConfiguration.IsEnabledWhenProfiling);
            Assert.Equal(options.SnapshotConfiguration.IsLowPrioritySnapshotUploader, deserializedOptions.SnapshotConfiguration.IsLowPrioritySnapshotUploader);
            Assert.Equal(options.SnapshotConfiguration.MaximumCollectionPlanSize, deserializedOptions.SnapshotConfiguration.MaximumCollectionPlanSize);
            Assert.Equal(options.SnapshotConfiguration.MaximumSnapshotsRequired, deserializedOptions.SnapshotConfiguration.MaximumSnapshotsRequired);
            Assert.Equal(options.SnapshotConfiguration.ProblemCounterResetInterval, deserializedOptions.SnapshotConfiguration.ProblemCounterResetInterval);
            Assert.Equal(options.SnapshotConfiguration.ProvideAnonymousTelemetry, deserializedOptions.SnapshotConfiguration.ProvideAnonymousTelemetry);
            Assert.Equal(options.SnapshotConfiguration.ReconnectInterval, deserializedOptions.SnapshotConfiguration.ReconnectInterval);
            Assert.Equal(options.SnapshotConfiguration.ShadowCopyFolder, deserializedOptions.SnapshotConfiguration.ShadowCopyFolder);
            Assert.Equal(options.SnapshotConfiguration.SnapshotInLowPriorityThread, deserializedOptions.SnapshotConfiguration.SnapshotInLowPriorityThread);
            Assert.Equal(options.SnapshotConfiguration.SnapshotsPerDayLimit, deserializedOptions.SnapshotConfiguration.SnapshotsPerDayLimit);
            Assert.Equal(options.SnapshotConfiguration.SnapshotsPerTenMinutesLimit, deserializedOptions.SnapshotConfiguration.SnapshotsPerTenMinutesLimit);
            Assert.Equal(options.SnapshotConfiguration.TempFolder, deserializedOptions.SnapshotConfiguration.TempFolder);
            Assert.Equal(options.SnapshotConfiguration.ThresholdForSnapshotting, deserializedOptions.SnapshotConfiguration.ThresholdForSnapshotting);

            Assert.Equal(options.DependencyTrackingOptions.SetComponentCorrelationHttpHeaders, deserializedOptions.DependencyTrackingOptions.SetComponentCorrelationHttpHeaders);
            Assert.Equal(options.DependencyTrackingOptions.DisableDiagnosticSourceInstrumentation, deserializedOptions.DependencyTrackingOptions.DisableDiagnosticSourceInstrumentation);
            Assert.Equal(options.DependencyTrackingOptions.DisableRuntimeInstrumentation, deserializedOptions.DependencyTrackingOptions.DisableRuntimeInstrumentation);
            Assert.Equal(options.DependencyTrackingOptions.EnableAzureSdkTelemetryListener, deserializedOptions.DependencyTrackingOptions.EnableAzureSdkTelemetryListener);
            Assert.Equal(options.DependencyTrackingOptions.EnableLegacyCorrelationHeadersInjection, deserializedOptions.DependencyTrackingOptions.EnableLegacyCorrelationHeadersInjection);
            Assert.Equal(options.DependencyTrackingOptions.EnableRequestIdHeaderInjectionInW3CMode, deserializedOptions.DependencyTrackingOptions.EnableRequestIdHeaderInjectionInW3CMode);
            Assert.Equal(options.DependencyTrackingOptions.EnableSqlCommandTextInstrumentation, deserializedOptions.DependencyTrackingOptions.EnableSqlCommandTextInstrumentation);
            Assert.Equal("*******", deserializedOptions.TokenCredentialOptions.ClientId);
            Assert.Equal(options.EnableLiveMetrics, deserializedOptions.EnableLiveMetrics);
            Assert.Equal(options.EnableLiveMetricsFilters, deserializedOptions.EnableLiveMetricsFilters);
            Assert.Equal(false, deserializedOptions.EnableMetricsCustomDimensionOptimization);
        }

        [Fact]
        public void ApplicationInsights_RequestIsTrackedIfActivityIsIgnored()
        {
            var result = CreateDefaultInstanceLogEntry();
            var logger = CreateLogger(LogCategories.Results);

            Activity parent = new Activity("foo").Start();
            using (logger.BeginScope(new Dictionary<string, object> { ["MS_IgnoreActivity"] = null }))
            using (logger.BeginFunctionScope(CreateFunctionInstance(_invocationId), _hostInstanceId))
            {
                logger.LogFunctionResult(result);
            }

            Assert.Single(_channel.Telemetries.OfType<RequestTelemetry>());
            RequestTelemetry telemetry = _channel.Telemetries.OfType<RequestTelemetry>().Single();

            Assert.NotEqual(telemetry.Id, parent.Id);
        }

        [Fact]
        public void ApplicationInsights_RequestIsNotTrackedIfActivityIsNotIgnored()
        {
            var result = CreateDefaultInstanceLogEntry();
            var logger = CreateLogger(LogCategories.Results);

            Activity parent = new Activity("foo").Start();
            using (logger.BeginFunctionScope(CreateFunctionInstance(_invocationId), _hostInstanceId))
            {
                logger.LogFunctionResult(result);
            }

            // ApplicationInsights auto-tracks telemetry, functions do not track it.
            Assert.Empty(_channel.Telemetries.OfType<RequestTelemetry>());
        }

        [Fact]
        public void ApplicationInsights_RequestIsTrackedIfTrackActivityIsSpecified()
        {
            var result = CreateDefaultInstanceLogEntry();
            var logger = CreateLogger(LogCategories.Results);

            Activity current = new Activity("foo");
            current.Start();
            using (logger.BeginScope(new Dictionary<string, object> { ["MS_TrackActivity"] = null }))
            using (logger.BeginFunctionScope(CreateFunctionInstance(_invocationId), _hostInstanceId))
            {
                logger.LogFunctionResult(result);
            }

            Assert.Single(_channel.Telemetries.OfType<RequestTelemetry>());
            RequestTelemetry telemetry = _channel.Telemetries.OfType<RequestTelemetry>().Single();
            Assert.Equal(current.SpanId.ToHexString(), telemetry.Id);
        }

        [Fact]
        public async Task ApplicationInsights_RequestHasSourceIfTriggerDetailsArePopulated()
        {
            var result = CreateDefaultInstanceLogEntry();
            ILogger logger = CreateLogger(LogCategories.Results);

            var functionInstance = CreateFunctionInstance(_invocationId, new Dictionary<string, string>
            {
                ["Endpoint"] = "my-eventhub.somewhere-in-azure.com",
                ["Entity"] = "images",
            });

            using (logger.BeginFunctionScope(functionInstance, _hostInstanceId))
            {
                // sleep briefly to provide a non-zero Duration
                await Task.Delay(100);
                logger.LogFunctionResult(result);
            }

            RequestTelemetry telemetry = _channel.Telemetries.Single() as RequestTelemetry;

            Assert.Equal("my-eventhub.somewhere-in-azure.com/images", telemetry.Source);
        }


        [Fact]
        public async Task ApplicationInsights_RequestHasSourceIfTriggerEndpointIsPopulated()
        {
            var result = CreateDefaultInstanceLogEntry();
            ILogger logger = CreateLogger(LogCategories.Results);

            var functionInstance = CreateFunctionInstance(_invocationId, new Dictionary<string, string>
            {
                ["Endpoint"] = "my-eventhub.somewhere-in-azure.com",
            });

            using (logger.BeginFunctionScope(functionInstance, _hostInstanceId))
            {
                // sleep briefly to provide a non-zero Duration
                await Task.Delay(100);
                logger.LogFunctionResult(result);
            }

            RequestTelemetry telemetry = _channel.Telemetries.Single() as RequestTelemetry;

            Assert.Equal("my-eventhub.somewhere-in-azure.com", telemetry.Source);
        }

        [Fact]
        public async Task ApplicationInsights_RequestHasSourceIfTriggerEntityIsPopulated()
        {
            var result = CreateDefaultInstanceLogEntry();
            ILogger logger = CreateLogger(LogCategories.Results);

            var functionInstance = CreateFunctionInstance(_invocationId, new Dictionary<string, string>
            {
                ["Entity"] = "images",
            });

            using (logger.BeginFunctionScope(functionInstance, _hostInstanceId))
            {
                // sleep briefly to provide a non-zero Duration
                await Task.Delay(100);
                logger.LogFunctionResult(result);
            }

            RequestTelemetry telemetry = _channel.Telemetries.Single() as RequestTelemetry;

            Assert.Equal("images", telemetry.Source);
        }

        [Fact]
        public static void ApplicationInsights_ApplyProperty_CaseSensitive()
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();

            // when applying prefixing, each different case pattern will be its own property
            ApplicationInsightsLogger.ApplyProperty(properties, "Test", "1", applyPrefix: true);
            ApplicationInsightsLogger.ApplyProperty(properties, "test", "2", applyPrefix: true);

            // last one wins
            ApplicationInsightsLogger.ApplyProperty(properties, "tEsT", "3", applyPrefix: true);
            ApplicationInsightsLogger.ApplyProperty(properties, "tEsT", "4", applyPrefix: true);

            // no prefixing - still case sensitive
            ApplicationInsightsLogger.ApplyProperty(properties, "TEST", "5", applyPrefix: false);
            ApplicationInsightsLogger.ApplyProperty(properties, "TESt", "6", applyPrefix: false);

            Assert.Equal(5, properties.Count);

            Assert.Equal("1", properties["prop__Test"]);
            Assert.Equal("2", properties["prop__test"]);
            Assert.Equal("4", properties["prop__tEsT"]);
            Assert.Equal("5", properties["TEST"]);
            Assert.Equal("6", properties["TESt"]);
        }

        private async Task Level1(Guid asyncLocalSetting)
        {
            // Push and pop values onto the dictionary at various levels. Make sure they
            // maintain their AsyncLocal state
            var level1 = new Dictionary<string, object>
            {
                ["AsyncLocal"] = asyncLocalSetting,
                ["1"] = 1,
                ["shared"] = "Level1"
            };

            ILogger logger = CreateLogger(_functionCategoryName);
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
                ["2"] = 2,
                ["shared"] = "Level2"
            };

            var expectedLevel2 = new Dictionary<string, object>
            {
                ["1"] = 1,
                ["2"] = 2,
                ["AsyncLocal"] = asyncLocalSetting,
                ["shared"] = "Level2"
            };

            ILogger logger2 = CreateLogger(_functionCategoryName);
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
                ["3"] = 3,
                ["shared"] = "Level3"
            };

            var expectedLevel3 = new Dictionary<string, object>
            {
                ["1"] = 11,
                ["2"] = 2,
                ["3"] = 3,
                ["AsyncLocal"] = asyncLocalSetting,
                ["shared"] = "Level3"
            };

            ILogger logger3 = CreateLogger(_functionCategoryName);
            using (logger3.BeginScope(level3))
            {
                ValidateScope(expectedLevel3);
            }
        }

        private static void MockHttpRequest(Mock<HttpRequest> request, string ipAddress, IDictionary<object, object> items = null)
        {
            var connectionInfoMock = new Mock<ConnectionInfo>();
            connectionInfoMock.SetupGet(c => c.RemoteIpAddress).Returns(IPAddress.Parse(ipAddress));

            var contextMock = new Mock<HttpContext>();
            contextMock.SetupGet(c => c.Connection).Returns(connectionInfoMock.Object);

            request.SetupGet(r => r.HttpContext).Returns(contextMock.Object);

            items = items ?? new Dictionary<object, object>();
            contextMock.SetupGet(c => c.Items).Returns(items);
        }

        private ILogger CreateLogger(string category)
        {
            return new ApplicationInsightsLogger(_client, category, new ApplicationInsightsLoggerOptions());
        }

        private static void ValidateScope(IDictionary<string, object> expected)
        {
            var scopeDict = DictionaryLoggerScope.GetMergedStateDictionaryOrNull();
            Assert.Equal(expected.Count, scopeDict.Count);
            foreach (var entry in expected)
            {
                Assert.Equal(entry.Value, scopeDict[entry.Key]);
            }
        }

        private IFunctionInstance CreateFunctionInstance(Guid id, Dictionary<string, string> triggerDetails = null)
        {
            var method = GetType().GetMethod(nameof(TestFunction), BindingFlags.NonPublic | BindingFlags.Static);
            var descriptor = FunctionIndexer.FromMethod(method, new ConfigurationBuilder().Build());
            var serviceScopeFactoryMock = new Mock<IServiceScopeFactory>(MockBehavior.Strict);

            return new FunctionInstance(id, triggerDetails ?? new Dictionary<string, string>(), null, new ExecutionReason(), null, null, descriptor, serviceScopeFactoryMock.Object);
        }

        private static void TestFunction()
        {
            // used for a FunctionDescriptor
        }

        private FunctionInstanceLogEntry CreateDefaultInstanceLogEntry(Exception ex = null)
        {
            return new FunctionInstanceLogEntry
            {
                FunctionName = _functionFullName,
                LogName = _functionShortName,
                FunctionInstanceId = _invocationId,
                StartTime = _startTime,
                EndTime = _endTime,
                LogOutput = "a bunch of output that we will not forward", // not used here -- this is all Traced
                TriggerReason = _triggerReason,
                ParentId = Guid.NewGuid(), // we do not track this
                ErrorDetails = null, // we do not use this -- we pass the exception in separately
                Arguments = _arguments,
                Duration = TimeSpan.FromMilliseconds(_durationMs),
                Exception = ex
            };
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _host?.Dispose();

            while (Activity.Current != null)
            {
                Activity.Current.Stop();
            }
        }
    }
}