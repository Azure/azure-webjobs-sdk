using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.ApplicationInsights.WindowsServer.Channel.Implementation;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    public class ApplicationInsightsEndToEndTests
    {
        private const string _mockApplicationInsightsUrl = "http://localhost:4005/v2/track/";
        private const string _mockQuickPulseUrl = "http://localhost:4005/QuickPulseService.svc/";

        private readonly TestTelemetryChannel _channel = new TestTelemetryChannel();
        private const string _mockApplicationInsightsKey = "some_key";
        private const string _customScopeKey = "MyCustomScopeKey";
        private const string _customScopeValue = "MyCustomScopeValue";

        private const string _dateFormat = "HH':'mm':'ss'.'fffK";

        [Fact]
        public async Task ApplicationInsights_SuccessfulFunction()
        {
            string testName = nameof(TestApplicationInsightsInformation);
            LogCategoryFilter filter = new LogCategoryFilter();
            filter.DefaultLevel = LogLevel.Information;

            var loggerFactory = new LoggerFactory()
                .AddApplicationInsights(
                    new TestTelemetryClientFactory(filter.Filter, _channel));

            JobHostConfiguration config = new JobHostConfiguration
            {
                LoggerFactory = loggerFactory,
                TypeLocator = new FakeTypeLocator(GetType()),
            };
            config.Aggregator.IsEnabled = false;

            using (JobHost host = new JobHost(config))
            {
                await host.StartAsync();
                var methodInfo = GetType().GetMethod(testName, BindingFlags.Public | BindingFlags.Static);
                await host.CallAsync(methodInfo, new { input = "function input" });
                await host.StopAsync();
            }

            Assert.Equal(8, _channel.Telemetries.Count);

            // Validate the traces. Order by message string as the requests may come in
            // slightly out-of-order or on different threads
            TraceTelemetry[] telemetries = _channel.Telemetries
                .OfType<TraceTelemetry>()
                .OrderBy(t => t.Message)
                .ToArray();

            ValidateTrace(telemetries[0], "Found the following functions:\r\n", LogCategories.Startup);
            ValidateTrace(telemetries[1], "Job host started", LogCategories.Startup);
            ValidateTrace(telemetries[2], "Job host stopped", LogCategories.Startup);
            ValidateTrace(telemetries[3], "Logger", LogCategories.Function, testName, hasCustomScope: true);
            ValidateTrace(telemetries[4], "ServicePointManager.DefaultConnectionLimit", LogCategories.Startup, expectedLevel: SeverityLevel.Warning);
            ValidateTrace(telemetries[5], "Trace", LogCategories.Function, testName);

            // We should have 1 custom metric.
            MetricTelemetry metric = _channel.Telemetries
                .OfType<MetricTelemetry>()
                .Single();
            ValidateMetric(metric, testName);

            // Finally, validate the request
            RequestTelemetry request = _channel.Telemetries
                .OfType<RequestTelemetry>()
                .Single();
            ValidateRequest(request, testName, true);
        }

        [Fact]
        public async Task ApplicationInsights_FailedFunction()
        {
            string testName = nameof(TestApplicationInsightsFailure);
            LogCategoryFilter filter = new LogCategoryFilter();
            filter.DefaultLevel = LogLevel.Information;

            var loggerFactory = new LoggerFactory()
                .AddApplicationInsights(
                    new TestTelemetryClientFactory(filter.Filter, _channel));

            JobHostConfiguration config = new JobHostConfiguration
            {
                LoggerFactory = loggerFactory,
                TypeLocator = new FakeTypeLocator(GetType()),
            };
            config.Aggregator.IsEnabled = false;

            using (JobHost host = new JobHost(config))
            {
                await host.StartAsync();
                var methodInfo = GetType().GetMethod(testName, BindingFlags.Public | BindingFlags.Static);
                await Assert.ThrowsAsync<FunctionInvocationException>(() => host.CallAsync(methodInfo, new { input = "function input" }));
                await host.StopAsync();
            }

            Assert.Equal(10, _channel.Telemetries.Count);

            // Validate the traces. Order by message string as the requests may come in
            // slightly out-of-order or on different threads
            TraceTelemetry[] telemetries = _channel.Telemetries
                .OfType<TraceTelemetry>()
                .OrderBy(t => t.Message)
                .ToArray();

            ValidateTrace(telemetries[0], "Error", LogCategories.Function, testName, expectedLevel: SeverityLevel.Error);
            ValidateTrace(telemetries[1], "Found the following functions:\r\n", LogCategories.Startup);
            ValidateTrace(telemetries[2], "Job host started", LogCategories.Startup);
            ValidateTrace(telemetries[3], "Job host stopped", LogCategories.Startup);
            ValidateTrace(telemetries[4], "Logger", LogCategories.Function, testName, hasCustomScope: true);
            ValidateTrace(telemetries[5], "ServicePointManager.DefaultConnectionLimit", LogCategories.Startup, expectedLevel: SeverityLevel.Warning);
            ValidateTrace(telemetries[6], "Trace", LogCategories.Function, testName);

            // Validate the exception
            ExceptionTelemetry[] exceptions = _channel.Telemetries
                .OfType<ExceptionTelemetry>()
                .OrderBy(t => t.Timestamp)
                .ToArray();
            Assert.Equal(2, exceptions.Length);
            ValidateException(exceptions[0], LogCategories.Function, testName);
            ValidateException(exceptions[1], LogCategories.Results, testName);

            // Finally, validate the request
            RequestTelemetry request = _channel.Telemetries
                .OfType<RequestTelemetry>()
                .Single();
            ValidateRequest(request, testName, false);
        }

        [Theory]
        [InlineData(LogLevel.None, 0)]
        [InlineData(LogLevel.Information, 19)]
        [InlineData(LogLevel.Warning, 11)]
        public async Task QuickPulse_Works_EvenIfFiltered(LogLevel defaultLevel, int expectedTelemetryItems)
        {
            LogCategoryFilter filter = new LogCategoryFilter();
            filter.DefaultLevel = defaultLevel;
            TestLoggerProvider testLoggerProvider = new TestLoggerProvider();

            var loggerFactory = new LoggerFactory()
                .AddApplicationInsights(
                    new TestTelemetryClientFactory(filter.Filter, _channel));

            loggerFactory.AddProvider(testLoggerProvider);

            JobHostConfiguration config = new JobHostConfiguration
            {
                LoggerFactory = loggerFactory,
                TypeLocator = new FakeTypeLocator(GetType()),
            };
            config.Aggregator.IsEnabled = false;

            using (var qpListener = new QuickPulseEventListener())
            using (var listener = new ApplicationInsightsTestListener())
            {
                listener.StartListening();

                int requests = 5;
                using (JobHost host = new JobHost(config))
                {
                    await host.StartAsync();

                    // QuickPulse only tracks when there's an active listener. Wait until some
                    // real data starts flowing before kicking off the test scenario
                    await TestHelpers.Await(() => listener.IsReady);

                    var methodInfo = GetType().GetMethod(nameof(TestApplicationInsightsWarning), BindingFlags.Public | BindingFlags.Static);
                    List<Task> invokeTasks = new List<Task>();

                    for (int i = 0; i < requests; i++)
                    {
                        invokeTasks.Add(host.CallAsync(methodInfo));
                    }

                    await Task.WhenAll(invokeTasks);

                    // If we stop the host too early, the QuickPulse items may not all be flushed. So wait until 
                    // we see them before continuing. 
                    double min = requests - 2;
                    double? sum = null;

                    await TestHelpers.Await(() =>
                    {
                        // Sum up all req/sec calls that we've received.
                        var reqPerSec = listener.GetQuickPulseItems()
                           .Select(p => p.Metrics.Where(q => q.Name == @"\ApplicationInsights\Requests/Sec").Single());
                        sum = reqPerSec.Sum(p => p.Value);

                        // All requests will go to QuickPulse.
                        // The calculated RPS may off, so give some wiggle room. The important thing is that it's generating 
                        // RequestTelemetry and not being filtered.
                        return sum >= min;
                    }, timeout: 5000, pollingInterval: 100,
                    userMessageCallback: () =>
                    {
                        var items = listener.GetQuickPulseItems().OrderBy(i => i.Timestamp).Take(10);
                        var s = items.Select(i => $"[{i.Timestamp.ToString(_dateFormat)}] {i.Metrics.Single(p => p.Name == @"\ApplicationInsights\Requests/Sec")}");
                        return $"Expected sum to be greater than '{min}'. Actual: '{sum}'. DefaultLevel: '{defaultLevel}'.{Environment.NewLine}QuickPulse items ({items.Count()}): {string.Join(Environment.NewLine, s)}{Environment.NewLine}QuickPulse Logs:{qpListener.Log}{Environment.NewLine}Logs: {testLoggerProvider.GetLogString()}";
                    });

                    await host.StopAsync();
                }

                loggerFactory.Dispose();

                // These will be filtered based on the default filter.
                Assert.Equal(expectedTelemetryItems, _channel.Telemetries.Count());
            }
        }

        // Test Functions
        [NoAutomaticTrigger]
        public static void TestApplicationInsightsInformation(string input, TraceWriter trace, ILogger logger)
        {
            // Wrap in a custom scope with custom properties.
            using (logger.BeginScope(new Dictionary<string, object>
            {
                [_customScopeKey] = _customScopeValue
            }))
            {
                trace.Info("Trace");
                logger.LogInformation("Logger");

                logger.LogMetric("MyCustomMetric", 5.1, new Dictionary<string, object>
                {
                    ["MyCustomMetricProperty"] = 100,
                    ["Count"] = 50,
                    ["min"] = 10.4,
                    ["Max"] = 23
                });
            }
        }

        [NoAutomaticTrigger]
        public static void TestApplicationInsightsFailure(string input, TraceWriter trace, ILogger logger)
        {
            // Wrap in a custom scope with custom properties, using the structured logging approach.
            using (logger.BeginScope($"{{{_customScopeKey}}}", _customScopeValue))
            {
                trace.Info("Trace");
                logger.LogInformation("Logger");

                // Note: Exceptions thrown do *not* have the custom scope properties attached because
                // the logging doesn't occur until after the scope has left. Logging an Exception directly 
                // will have the proper scope attached.
                logger.LogError(0, new Exception("Boom 1!"), "Error");
                throw new Exception("Boom 2!");
            }
        }

        private static void ValidateMetric(MetricTelemetry telemetry, string expectedOperationName)
        {
            Assert.Equal(expectedOperationName, telemetry.Context.Operation.Name);
            Assert.NotNull(telemetry.Context.Operation.Id);
            Assert.Equal(LogCategories.Function, telemetry.Properties[LogConstants.CategoryNameKey]);
            Assert.Equal(LogLevel.Information.ToString(), telemetry.Properties[LogConstants.LogLevelKey]);

            Assert.Equal("MyCustomMetric", telemetry.Name);
            Assert.Equal(5.1, telemetry.Sum);
            Assert.Equal(50, telemetry.Count);
            Assert.Equal(10.4, telemetry.Min);
            Assert.Equal(23, telemetry.Max);
            Assert.Null(telemetry.StandardDeviation);
            Assert.Equal("100", telemetry.Properties[$"{LogConstants.CustomPropertyPrefix}MyCustomMetricProperty"]);
            ValidateCustomScopeProperty(telemetry);

            ValidateSdkVersion(telemetry);
        }

        private static void ValidateCustomScopeProperty(ISupportProperties telemetry)
        {
            Assert.Equal(_customScopeValue, telemetry.Properties[$"{LogConstants.CustomPropertyPrefix}{_customScopeKey}"]);
        }

        [NoAutomaticTrigger]
        public static void TestApplicationInsightsWarning(TraceWriter trace, ILogger logger)
        {
            trace.Warning("Trace");
            logger.LogWarning("Logger");
        }

        private class ApplicationInsightsTestListener : IDisposable
        {
            private readonly HttpListener _applicationInsightsListener = new HttpListener();
            private ConcurrentQueue<QuickPulsePayload> _quickPulseItems = new ConcurrentQueue<QuickPulsePayload>();
            private Thread _listenerThread;

            private bool _pingReceived = false;
            private int _dataReceived = 0;

            // We want to make sure the QuickPulse module has sent us a few requests before continuing
            public bool IsReady => _pingReceived && (_dataReceived >= 2);

            public IEnumerable<QuickPulsePayload> GetQuickPulseItems()
            {
                return _quickPulseItems.ToList();
            }

            public void StartListening()
            {
                _applicationInsightsListener.Prefixes.Add(_mockApplicationInsightsUrl);
                _applicationInsightsListener.Prefixes.Add(_mockQuickPulseUrl);
                _applicationInsightsListener.Start();
                Listen();
            }

            private void Listen()
            {
                // process a request, then continue to wait for the next
                _listenerThread = new Thread(() =>
                {
                    while (_applicationInsightsListener.IsListening)
                    {
                        try
                        {
                            HttpListenerContext context = _applicationInsightsListener.GetContext();
                            ProcessRequest(context);
                        }
                        catch (HttpListenerException)
                        {
                            // This happens when stopping the listener.
                        }
                    }
                });

                _listenerThread.Start();
            }

            private void ProcessRequest(HttpListenerContext context)
            {
                var request = context.Request;
                var response = context.Response;

                try
                {
                    if (request.Url.OriginalString.StartsWith(_mockQuickPulseUrl))
                    {
                        HandleQuickPulseRequest(request, response);
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
                finally
                {
                    response.Close();
                }
            }

            private void HandleQuickPulseRequest(HttpListenerRequest request, HttpListenerResponse response)
            {
                string result = GetRequestContent(request);
                response.AddHeader("x-ms-qps-subscribed", true.ToString());

                if (request.Url.LocalPath == "/QuickPulseService.svc/post")
                {
                    QuickPulsePayload[] quickPulse = JsonConvert.DeserializeObject<QuickPulsePayload[]>(result);
                    foreach (var i in quickPulse)
                    {
                        _quickPulseItems.Enqueue(i);
                    }

                    Interlocked.Increment(ref _dataReceived);
                }
                else if (request.Url.LocalPath == "/QuickPulseService.svc/ping")
                {
                    _pingReceived = true;
                }
            }

            private static string GetRequestContent(HttpListenerRequest request)
            {
                string result = null;
                if (request.HasEntityBody)
                {
                    using (var requestInputStream = request.InputStream)
                    {
                        var encoding = request.ContentEncoding;
                        using (var reader = new StreamReader(requestInputStream, encoding))
                        {
                            result = reader.ReadToEnd();
                        }
                    }
                }
                return result;
            }

            private static string Decompress(string content)
            {
                var zippedData = Encoding.Default.GetBytes(content);
                using (var ms = new MemoryStream(zippedData))
                {
                    using (var compressedzipStream = new GZipStream(ms, CompressionMode.Decompress))
                    {
                        var outputStream = new MemoryStream();
                        var block = new byte[1024];
                        while (true)
                        {
                            int bytesRead = compressedzipStream.Read(block, 0, block.Length);
                            if (bytesRead <= 0)
                            {
                                break;
                            }

                            outputStream.Write(block, 0, bytesRead);
                        }
                        compressedzipStream.Close();
                        return Encoding.UTF8.GetString(outputStream.ToArray());
                    }
                }
            }

            public void Dispose()
            {
                _applicationInsightsListener.Stop();
                _listenerThread.Join();
            }
        }

        private static void ValidateTrace(TraceTelemetry telemetry, string expectedMessageStartsWith, string expectedCategory,
            string expectedOperationName = null, bool hasCustomScope = false, SeverityLevel expectedLevel = SeverityLevel.Information)
        {
            Assert.StartsWith(expectedMessageStartsWith, telemetry.Message);
            Assert.Equal(expectedLevel, telemetry.SeverityLevel);

            Assert.Equal(expectedCategory, telemetry.Properties[LogConstants.CategoryNameKey]);

            if (hasCustomScope)
            {
                ValidateCustomScopeProperty(telemetry);
            }

            if (expectedCategory == LogCategories.Function || expectedCategory == LogCategories.Executor)
            {
                // These should have associated operation information
                Assert.Equal(expectedOperationName, telemetry.Context.Operation.Name);
                Assert.NotNull(telemetry.Context.Operation.Id);
            }
            else
            {
                Assert.Null(telemetry.Context.Operation.Name);
                Assert.Null(telemetry.Context.Operation.Id);
            }

            ValidateSdkVersion(telemetry);
        }

        private static void ValidateException(ExceptionTelemetry telemetry, string expectedCategory, string expectedOperationName)
        {
            Assert.Equal(expectedCategory, telemetry.Properties[LogConstants.CategoryNameKey]);
            Assert.Equal(expectedOperationName, telemetry.Context.Operation.Name);
            Assert.NotNull(telemetry.Context.Operation.Id);

            if (expectedCategory == LogCategories.Results)
            {
                // Check that the Function details show up as 'prop__'. We may change this in the future as
                // it may not be exceptionally useful.
                Assert.Equal(expectedOperationName, telemetry.Properties[$"{LogConstants.CustomPropertyPrefix}{LogConstants.NameKey}"]);
                Assert.Equal("This function was programmatically called via the host APIs.", telemetry.Properties[$"{LogConstants.CustomPropertyPrefix}{LogConstants.TriggerReasonKey}"]);

                Assert.IsType<FunctionInvocationException>(telemetry.Exception);
                Assert.IsType<Exception>(telemetry.Exception.InnerException);
            }
            else
            {
                Assert.IsType<Exception>(telemetry.Exception);

                // Result logs do not include custom scopes.
                ValidateCustomScopeProperty(telemetry);
            }

            ValidateSdkVersion(telemetry);
        }

        private static void ValidateRequest(RequestTelemetry telemetry, string operationName, bool success)
        {
            Assert.NotNull(telemetry.Context.Operation.Id);
            Assert.Equal(operationName, telemetry.Context.Operation.Name);
            Assert.NotNull(telemetry.Duration);
            Assert.Equal(success, telemetry.Success);

            Assert.Equal($"ApplicationInsightsEndToEndTests.{operationName}", telemetry.Properties[LogConstants.FullNameKey].ToString());
            Assert.Equal("This function was programmatically called via the host APIs.", telemetry.Properties[LogConstants.TriggerReasonKey].ToString());

            ValidateSdkVersion(telemetry);
        }

        private static void ValidateSdkVersion(ITelemetry telemetry)
        {
            PropertyInfo propInfo = typeof(TelemetryContext).GetProperty("Tags", BindingFlags.NonPublic | BindingFlags.Instance);
            IDictionary<string, string> tags = propInfo.GetValue(telemetry.Context) as IDictionary<string, string>;

            Assert.StartsWith("webjobs: ", tags["ai.internal.sdkVersion"]);
        }

        private class QuickPulsePayload
        {
            public string Instance { get; set; }

            public DateTime Timestamp { get; set; }

            public string StreamId { get; set; }

            public QuickPulseMetric[] Metrics { get; set; }

            public override string ToString()
            {
                string s = string.Join(Environment.NewLine, Metrics.Select(m => $"  {m}"));
                return $"[{Timestamp.ToString(_dateFormat)}] Metrics:{Environment.NewLine}{s}";
            }
        }

        private class QuickPulseMetric
        {
            public string Name { get; set; }

            public double Value { get; set; }

            public int Weight { get; set; }

            public override string ToString()
            {
                return $"{Name}: {Value} ({Weight})";
            }
        }

        // For debugging QuickPulse failures
        private class QuickPulseEventListener : EventListener
        {
            private readonly StringBuilder _sb = new StringBuilder();

            public string Log => _sb.ToString();

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                var trimmedData = eventData.Payload.ToList();
                trimmedData.RemoveAt(trimmedData.Count - 1);

                string log = string.Format(eventData.Message, trimmedData.ToArray());

                _sb.AppendLine($"[{DateTime.UtcNow.ToString(_dateFormat)}] {log}");
            }

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (eventSource.Name == "Microsoft-ApplicationInsights-Extensibility-PerformanceCollector-QuickPulse")
                {
                    EnableEvents(eventSource, EventLevel.LogAlways);
                }

                base.OnEventSourceCreated(eventSource);
            }
        }

        private class TestTelemetryClientFactory : DefaultTelemetryClientFactory
        {
            private TestTelemetryChannel _channel;

            public TestTelemetryClientFactory(Func<string, LogLevel, bool> filter, TestTelemetryChannel channel)
                : base(_mockApplicationInsightsKey, new SamplingPercentageEstimatorSettings(), filter)
            {
                _channel = channel;
            }

            protected override QuickPulseTelemetryModule CreateQuickPulseTelemetryModule()
            {
                QuickPulseTelemetryModule module = base.CreateQuickPulseTelemetryModule();
                module.QuickPulseServiceEndpoint = _mockQuickPulseUrl;
                return module;
            }

            protected override ITelemetryChannel CreateTelemetryChannel()
            {
                return _channel;
            }
        }

        private class TestTelemetryChannel : ITelemetryChannel
        {
            public ConcurrentBag<ITelemetry> Telemetries = new ConcurrentBag<ITelemetry>();

            public bool? DeveloperMode { get; set; }

            public string EndpointAddress { get; set; }

            public void Dispose()
            {
            }

            public void Flush()
            {
            }

            public void Send(ITelemetry item)
            {
                Telemetries.Add(item);
            }
        }
    }
}
