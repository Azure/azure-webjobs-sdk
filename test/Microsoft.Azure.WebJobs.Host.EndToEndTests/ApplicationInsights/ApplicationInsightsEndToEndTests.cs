// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

        private IHost ConfigureHost(LogCategoryFilter filter = null)
        {
            if (filter == null)
            {
                filter = new LogCategoryFilter();
                filter.DefaultLevel = LogLevel.Information;
            }

            var host = new HostBuilder()
                .ConfigureDefaultTestHost<ApplicationInsightsEndToEndTests>()
                .AddAzureStorage()
                .ConfigureServices(services =>
                {
                    services.Configure<FunctionResultAggregatorOptions>(o =>
                    {
                        o.IsEnabled = false;
                    });
                })
                .AddApplicationInsights(_mockApplicationInsightsKey, filter.Filter, null)
                .ConfigureServices(services =>
                {
                    var quickPulse = services.Single(s => s.ImplementationType == typeof(QuickPulseTelemetryModule));
                    services.Remove(quickPulse);
                    services.AddSingleton<ITelemetryModule>(new QuickPulseTelemetryModule()
                    {
                        QuickPulseServiceEndpoint = _mockQuickPulseUrl
                    });

                    var channel = services.Single(s => s.ServiceType == typeof(ITelemetryChannel));
                    services.Remove(channel);
                    services.AddSingleton<ITelemetryChannel>(_channel);
                })
                .Build();

            SetActiveTelemetryConfiguration(host.Services.GetService<TelemetryConfiguration>());
            return host;
        }

        [Fact]
        public async Task ApplicationInsights_SuccessfulFunction()
        {
            string testName = nameof(TestApplicationInsightsInformation);
            using (IHost host = ConfigureHost())
            {
                await host.StartAsync();
                var methodInfo = GetType().GetMethod(testName, BindingFlags.Public | BindingFlags.Static);
                await host.GetJobHost().CallAsync(methodInfo, new {input = "function input"});
                await host.StopAsync();

                Assert.Equal(11, _channel.Telemetries.Count);

                // Validate the request
                RequestTelemetry request = _channel.Telemetries
                    .OfType<RequestTelemetry>()
                    .Single();
                ValidateRequest(request, testName, true);

                // Validate the traces. Order by message string as the requests may come in
                // slightly out-of-order or on different threads
                TraceTelemetry[] telemetries = _channel.Telemetries
                    .OfType<TraceTelemetry>()
                    .OrderBy(t => t.Message)
                    .ToArray();

                string expectedFunctionCategory = LogCategories.CreateFunctionCategory(testName);
                string expectedFunctionUserCategory = LogCategories.CreateFunctionUserCategory(testName);

                ValidateTrace(telemetries[0], "Executed ", expectedFunctionCategory, testName);
                ValidateTrace(telemetries[1], "Executing ", expectedFunctionCategory, testName, request.Context.Operation.Id, request.Id);
                ValidateTrace(telemetries[2], "Found the following functions:\r\n", LogCategories.Startup);
                ValidateTrace(telemetries[3], "Job host started", LogCategories.Startup);
                ValidateTrace(telemetries[4], "Job host stopped", LogCategories.Startup);
                ValidateTrace(telemetries[5], "Logger", expectedFunctionUserCategory, testName, request.Context.Operation.Id, request.Id, hasCustomScope: true);
                ValidateTrace(telemetries[6], "Starting JobHost", "Microsoft.Azure.WebJobs.Hosting.JobHostService");
                ValidateTrace(telemetries[7], "Stopping JobHost", "Microsoft.Azure.WebJobs.Hosting.JobHostService");
                ValidateTrace(telemetries[8], "Trace", expectedFunctionUserCategory, testName, request.Context.Operation.Id, request.Id);

                // We should have 1 custom metric.
                MetricTelemetry metric = _channel.Telemetries
                    .OfType<MetricTelemetry>()
                    .Single();
                ValidateMetric(metric, testName);
            }
        }

        [Fact]
        public async Task ApplicationInsights_FailedFunction()
        {
            string testName = nameof(TestApplicationInsightsFailure);

            using (IHost host = ConfigureHost())
            {
                await host.StartAsync();
                var methodInfo = GetType().GetMethod(testName, BindingFlags.Public | BindingFlags.Static);
                await Assert.ThrowsAsync<FunctionInvocationException>(() => host.GetJobHost().CallAsync(methodInfo, new {input = "function input"}));
                await host.StopAsync();

                Assert.Equal(14, _channel.Telemetries.Count);

                // Validate the request
                RequestTelemetry request = _channel.Telemetries
                    .OfType<RequestTelemetry>()
                    .Single();
                ValidateRequest(request, testName, false);

                // Validate the traces. Order by message string as the requests may come in
                // slightly out-of-order or on different threads
                TraceTelemetry[] telemetries = _channel.Telemetries
                    .OfType<TraceTelemetry>()
                    .OrderBy(t => t.Message)
                    .ToArray();

                string expectedFunctionCategory = LogCategories.CreateFunctionCategory(testName);
                string expectedFunctionUserCategory = LogCategories.CreateFunctionUserCategory(testName);

                ValidateTrace(telemetries[0], "Error", expectedFunctionUserCategory, testName, request.Context.Operation.Id, request.Id,
                    expectedLogLevel: LogLevel.Error);
                ValidateTrace(telemetries[1], "Executed", expectedFunctionCategory, testName,
                    expectedLogLevel: LogLevel.Error);
                ValidateTrace(telemetries[2], "Executing", expectedFunctionCategory, testName, request.Context.Operation.Id, request.Id);
                ValidateTrace(telemetries[3], "Found the following functions:\r\n", LogCategories.Startup);
                ValidateTrace(telemetries[4], "Job host started", LogCategories.Startup);
                ValidateTrace(telemetries[5], "Job host stopped", LogCategories.Startup);
                ValidateTrace(telemetries[6], "Logger", expectedFunctionUserCategory, testName, request.Context.Operation.Id, request.Id, hasCustomScope: true);
                ValidateTrace(telemetries[7], "Starting JobHost", "Microsoft.Azure.WebJobs.Hosting.JobHostService");
                ValidateTrace(telemetries[8], "Stopping JobHost", "Microsoft.Azure.WebJobs.Hosting.JobHostService");
                ValidateTrace(telemetries[9], "Trace", expectedFunctionUserCategory, testName, request.Context.Operation.Id, request.Id);

                // Validate the exception
                ExceptionTelemetry[] exceptions = _channel.Telemetries
                    .OfType<ExceptionTelemetry>()
                    .OrderBy(t => t.Timestamp)
                    .ToArray();
                Assert.Equal(3, exceptions.Length);
                ValidateException(exceptions[0], expectedFunctionUserCategory, testName, request.Context.Operation.Id, request.Id);
                ValidateException(exceptions[1], LogCategories.Results, testName, request.Context.Operation.Id, request.Id);
                ValidateException(exceptions[2], expectedFunctionCategory, testName, null, null);
            }
        }

        [Theory]
        [InlineData(LogLevel.None, 0)]
        [InlineData(LogLevel.Information, 30)] // 3 start, 2 stop, 4x traces per request, 1x requests
        [InlineData(LogLevel.Warning, 10)] // 2x warning trace per request
        public async Task QuickPulse_Works_EvenIfFiltered(LogLevel defaultLevel, int expectedTelemetryItems)
        {
            LogCategoryFilter filter = new LogCategoryFilter();
            filter.DefaultLevel = defaultLevel;

            using (IHost host = ConfigureHost(filter))
            {
                var listener = new ApplicationInsightsTestListener();
                int requests = 5;

                try
                {
                    listener.StartListening();

                    JobHost jobHost = host.GetJobHost();
                    {
                        await host.StartAsync();

                        // let quick pulse start
                        await Task.Delay(5000);

                        var methodInfo = GetType().GetMethod(nameof(TestApplicationInsightsWarning),
                            BindingFlags.Public | BindingFlags.Static);

                        for (int i = 0; i < requests; i++)
                        {
                            await jobHost.CallAsync(methodInfo);
                        }

                        // let host run and proccess items asyncronously
                        await Task.Delay(2000);
                        await host.StopAsync();
                    }
                }
                finally
                {
                    listener.StopListening();
                }

                // wait for everything to flush
                await Task.Delay(2000);

                Assert.Equal(expectedTelemetryItems, _channel.Telemetries.Count);

                Assert.True(listener.QuickPulseItems.Count > 0);
                // Sum up all req/sec calls that we've received.
                var reqPerSec = listener
                    .QuickPulseItems.Select(p =>
                        p.Metrics.Single(q => q.Name == @"\ApplicationInsights\Requests/Sec")).ToList();

                Assert.True(reqPerSec.Count > 0);
                double sum = reqPerSec.Sum(p => p.Value);

                // All requests will go to QuickPulse.
                // The calculated RPS may off, so give some wiggle room. The important thing is that it's generating 
                // RequestTelemetry and not being filtered.

                double max = requests + 3;
                double min = requests - 2;
                Assert.True(sum > min && sum < max,
                    $"Expected sum to be greater than {min} and less than {max}. DefaultLevel: {defaultLevel}. Actual: {sum}");
            }
        }

        [Fact]
        public async Task ApplicationInsights_AIUsedExplicitlyFromFunctionCode()
        {
            string testName = nameof(TestApplicationInsightsExplicitCall);
            using (IHost host = ConfigureHost())
            {
                await host.StartAsync();
                var methodInfo = GetType().GetMethod(testName, BindingFlags.Public | BindingFlags.Static);
                await host.GetJobHost().CallAsync(methodInfo, null);
                await host.StopAsync();

                EventTelemetry[] eventTelemetries = _channel.Telemetries.OfType<EventTelemetry>().ToArray();
                Assert.Single(eventTelemetries);

                RequestTelemetry requestTelemetry = _channel.Telemetries.OfType<RequestTelemetry>().Single();

                Assert.Equal(requestTelemetry.Context.Operation.Name, eventTelemetries[0].Context.Operation.Name);
                Assert.Equal(requestTelemetry.Context.Operation.Id, eventTelemetries[0].Context.Operation.Id);
                Assert.Equal(requestTelemetry.Id, eventTelemetries[0].Context.Operation.ParentId);
            }
        }

        [Fact]
        public async Task ApplicationInsights_HttpTriggerIsTrackedOnce()
        {
            string testName = nameof(TestApplicationInsightsInformation);
            using (IHost host = ConfigureHost())
            {
                var telemetryClient = host.Services.GetService<TelemetryClient>();
                await host.StartAsync();
                var methodInfo = GetType().GetMethod(testName, BindingFlags.Public | BindingFlags.Static);

                RequestTelemetry outerRequest = null;

                // simulate auto tracked HTTP incoming call
                using (var operation = telemetryClient.StartOperation<RequestTelemetry>("GET /"))
                {
                    outerRequest = operation.Telemetry;
                    outerRequest.Success = true;
                    await host.GetJobHost().CallAsync(methodInfo, new { input = "input" });
                }

                await host.StopAsync();

                // Validate the request
                // There must be only one reported by the function code
                RequestTelemetry[] requestTelemetries = _channel.Telemetries.OfType<RequestTelemetry>().ToArray();
                Assert.Single(requestTelemetries);


                // and outer one, we started in the test should not - it does not have category and log level
                RequestTelemetry funtionRequest = requestTelemetries.Single();
                Assert.NotSame(outerRequest, funtionRequest);

                ValidateRequest(funtionRequest, testName, true);
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
                logger.LogError(new Exception("Boom 1!"), "Error");
                throw new Exception("Boom 2!");
            }
        }

        [NoAutomaticTrigger]
        public static void TestApplicationInsightsWarning(TraceWriter trace, ILogger logger)
        {
            trace.Warning("Trace");
            logger.LogWarning("Logger");
        }

        [NoAutomaticTrigger]
        public static void TestApplicationInsightsExplicitCall(ILogger logger)
        {
            var telemetryClient = new TelemetryClient(); // use TelemetryConfiguration.Active
            telemetryClient.TrackEvent("custom event");
        }

        private static void ValidateMetric(MetricTelemetry telemetry, string expectedOperationName)
        {
            Assert.Equal(expectedOperationName, telemetry.Context.Operation.Name);
            Assert.NotNull(telemetry.Context.Operation.Id);
            Assert.Equal(LogCategories.CreateFunctionUserCategory(expectedOperationName), telemetry.Properties[LogConstants.CategoryNameKey]);
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

        private class ApplicationInsightsTestListener
        {

            private readonly HttpListener _applicationInsightsListener = new HttpListener();
            private Task _listenTask;

            public ConcurrentQueue<QuickPulsePayload> QuickPulseItems { get; } = new ConcurrentQueue<QuickPulsePayload>();

            private CancellationTokenSource _tcs;
            public void StartListening()
            {
                _tcs = new CancellationTokenSource();
                _applicationInsightsListener.Prefixes.Add(_mockApplicationInsightsUrl);
                _applicationInsightsListener.Prefixes.Add(_mockQuickPulseUrl);
                _applicationInsightsListener.Start();
                _listenTask = Listen(_tcs.Token);
            }

            public void StopListening()
            {
                _applicationInsightsListener.Stop();
                _tcs?.Cancel(false);
                if (_listenTask != null && !_listenTask.IsCompleted)
                {
                    _listenTask.Wait();
                }

                _tcs?.Dispose();
                _listenTask = null;
            }

            private Task Listen(CancellationToken cancellationToken)
            {
                // process a request, then continue to wait for the next
                return Task.Run(async () => 
                {
                    while (_applicationInsightsListener.IsListening && !cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            HttpListenerContext context = await _applicationInsightsListener.GetContextAsync().ConfigureAwait(false);
                            ProcessRequest(context);
                        }
                        catch (Exception e) when (e is ObjectDisposedException || e is HttpListenerException)
                        {
                            // This can happen when stopping the listener.
                        }
                    }
                });
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
                        QuickPulseItems.Enqueue(i);
                    }
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
        }

        private static void ValidateTrace(TraceTelemetry telemetry, string expectedMessageStartsWith, string expectedCategory,
            string expectedOperationName = null, string expectedOperationId = null, string expectedParentId = null,
            bool hasCustomScope = false, LogLevel expectedLogLevel = LogLevel.Information)
        {
            Assert.StartsWith(expectedMessageStartsWith, telemetry.Message);
            Assert.Equal(GetSeverityLevel(expectedLogLevel), telemetry.SeverityLevel);

            Assert.Equal(expectedCategory, telemetry.Properties[LogConstants.CategoryNameKey]);

            if (hasCustomScope)
            {
                ValidateCustomScopeProperty(telemetry);
            }

            if (expectedCategory == LogCategories.CreateFunctionCategory(expectedOperationName) ||
                expectedCategory == LogCategories.CreateFunctionUserCategory(expectedOperationName))
            {
                // These should have associated operation information
                Assert.Equal(expectedOperationName, telemetry.Context.Operation.Name);
                Assert.Equal(expectedOperationId, telemetry.Context.Operation.Id);
                Assert.Equal(expectedParentId, telemetry.Context.Operation.ParentId);
            }
            else
            {
                Assert.Null(telemetry.Context.Operation.Name);
                Assert.Null(telemetry.Context.Operation.Id);
                Assert.Null(telemetry.Context.Operation.ParentId);
            }

            ValidateSdkVersion(telemetry);
        }

        private static SeverityLevel GetSeverityLevel(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                    return SeverityLevel.Verbose;
                case LogLevel.Information:
                    return SeverityLevel.Information;
                case LogLevel.Warning:
                    return SeverityLevel.Warning;
                case LogLevel.Error:
                    return SeverityLevel.Error;
                case LogLevel.Critical:
                    return SeverityLevel.Critical;
                case LogLevel.None:
                default:
                    throw new InvalidOperationException();
            }
        }

        private static void ValidateException(
            ExceptionTelemetry telemetry, 
            string expectedCategory, 
            string expectedOperationName,
            string expectedOperationId,
            string expectedParentId)
        {
            Assert.Equal(expectedCategory, telemetry.Properties[LogConstants.CategoryNameKey]);
            Assert.Equal(expectedOperationName, telemetry.Context.Operation.Name);
            Assert.Equal(expectedOperationId, telemetry.Context.Operation.Id);
            Assert.Equal(expectedParentId, telemetry.Context.Operation.ParentId);

            if (expectedCategory == LogCategories.CreateFunctionUserCategory(expectedOperationName))
            {
                // It came directly from the user
                Assert.IsType<Exception>(telemetry.Exception);

                // Result logs do not include custom scopes.
                ValidateCustomScopeProperty(telemetry);
            }
            else if (expectedCategory == LogCategories.CreateFunctionCategory(expectedOperationName))
            {
                // It came directly from the host, so wrapped in a FunctionInvocationException
                Assert.IsType<FunctionInvocationException>(telemetry.Exception);
            }
            else if (expectedCategory == LogCategories.Results)
            {
                // Check that the Function details show up as 'prop__'. We may change this in the future as
                // it may not be exceptionally useful.
                Assert.Equal(expectedOperationName, telemetry.Properties[$"{LogConstants.CustomPropertyPrefix}{LogConstants.NameKey}"]);
                Assert.Equal("This function was programmatically called via the host APIs.", telemetry.Properties[$"{LogConstants.CustomPropertyPrefix}{LogConstants.TriggerReasonKey}"]);

                Assert.IsType<FunctionInvocationException>(telemetry.Exception);
                Assert.IsType<Exception>(telemetry.Exception.InnerException);
            }

            ValidateSdkVersion(telemetry);
        }

        private static void ValidateRequest(RequestTelemetry telemetry, string operationName, bool success)
        {
            Assert.NotNull(telemetry.Context.Operation.Id);
            Assert.Equal(operationName, telemetry.Context.Operation.Name);
            Assert.NotNull(telemetry.Duration);
            Assert.Equal(success, telemetry.Success);

            Assert.Equal($"ApplicationInsightsEndToEndTests.{operationName}", telemetry.Properties[LogConstants.FullNameKey]);
            Assert.Equal("This function was programmatically called via the host APIs.", telemetry.Properties[LogConstants.TriggerReasonKey]);

            ValidateSdkVersion(telemetry);
        }

        private static void ValidateSdkVersion(ITelemetry telemetry)
        {
            Assert.StartsWith("webjobs: ", telemetry.Context.GetInternalContext().SdkVersion);
        }

        private class QuickPulsePayload
        {
            public string Instance { get; set; }

            public DateTime Timestamp { get; set; }

            public string StreamId { get; set; }

            public QuickPulseMetric[] Metrics { get; set; }
        }

        private class QuickPulseMetric
        {
            public string Name { get; set; }

            public double Value { get; set; }

            public int Weight { get; set; }
        }

        private void SetActiveTelemetryConfiguration(TelemetryConfiguration value)
        {
            TelemetryConfiguration.Active.Dispose();

            MethodInfo setActive =
                typeof(TelemetryConfiguration).GetMethod("set_Active", BindingFlags.Static | BindingFlags.NonPublic);

            setActive.Invoke(null, new object[] {value});
        }

        public void Dispose()
        {
            _channel?.Dispose();
        }
    }
}