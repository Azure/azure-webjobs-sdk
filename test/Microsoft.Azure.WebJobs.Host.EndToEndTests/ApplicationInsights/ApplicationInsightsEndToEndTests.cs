// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.AspNetCore;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Azure.WebJobs.Host.EndToEndTests.ApplicationInsights;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    public class ApplicationInsightsEndToEndTests : IDisposable, IClassFixture<ApplicationInsightsEndToEndTests.CustomTestWebHostFactory>
    {
        private const string _mockApplicationInsightsUrl = "http://localhost:4005/v2/track/";
        private const string _mockQuickPulseUrl = "http://localhost:4005/QuickPulseService.svc/";

        private readonly TestTelemetryChannel _channel = new TestTelemetryChannel();

        private const string _mockApplicationInsightsKey = "some_key";
        private const string _customScopeKey = "MyCustomScopeKey";
        private const string _customScopeValue = "MyCustomScopeValue";

        private const string _dateFormat = "HH':'mm':'ss'.'fffZ";
        private const int _expectedResponseCode = 204;

        private readonly CustomTestWebHostFactory _factory;
        private static RequestTrackingTelemetryModule _requestModuleForFirstRequest;

        public ApplicationInsightsEndToEndTests(CustomTestWebHostFactory factory)
        {
            _factory = factory;
            _requestModuleForFirstRequest = new RequestTrackingTelemetryModule();
        }

        private IHost ConfigureHost(LogLevel minLevel = LogLevel.Information, HttpAutoCollectionOptions httpOptions = null)
        {
            if (httpOptions == null)
            {
                httpOptions = new HttpAutoCollectionOptions();
            }

            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<ApplicationInsightsEndToEndTests>()
                .ConfigureServices(services =>
                {
                    services.Configure<FunctionResultAggregatorOptions>(o =>
                    {
                        o.IsEnabled = false;
                    });
                })
                .ConfigureLogging(b =>
                {
                    b.SetMinimumLevel(minLevel);
                    b.AddApplicationInsights(o =>
                    {
                        o.InstrumentationKey = _mockApplicationInsightsKey;
                        o.HttpAutoCollectionOptions = httpOptions;
                    });
                })
                .ConfigureServices(services =>
                {
                    ServiceDescriptor quickPulse = services.Single(s => s.ImplementationType == typeof(QuickPulseTelemetryModule));
                    services.Remove(quickPulse);
                    services.AddSingleton<ITelemetryModule, QuickPulseTelemetryModule>(s => new QuickPulseTelemetryModule()
                    {
                        QuickPulseServiceEndpoint = _mockQuickPulseUrl
                    });

                    ServiceDescriptor channel = services.Single(s => s.ServiceType == typeof(ITelemetryChannel));
                    services.Remove(channel);
                    services.AddSingleton<ITelemetryChannel>(_channel);
                })
                .Build();

            TelemetryConfiguration.Active.TelemetryChannel = _channel;
            return host;
        }

        [Fact]
        public async Task ApplicationInsights_SuccessfulFunction()
        {
            string testName = nameof(TestApplicationInsightsInformation);
            using (IHost host = ConfigureHost())
            {
                await host.StartAsync();
                MethodInfo methodInfo = GetType().GetMethod(testName, BindingFlags.Public | BindingFlags.Static);
                await host.GetJobHost().CallAsync(methodInfo, new { input = "function input" });
                await host.StopAsync();

                Assert.Equal(16, _channel.Telemetries.Count);

                // Validate the request
                RequestTelemetry request = _channel.Telemetries
                    .OfType<RequestTelemetry>()
                    .Single();
                ValidateRequest(request, testName, testName, null, null, true);

                // invocation id is retrievable from the request
                request.Properties.TryGetValue(LogConstants.InvocationIdKey, out string invocationId);

                // Validate the traces. Order by message string as the requests may come in
                // slightly out-of-order or on different threads
                TraceTelemetry[] telemetries = _channel.Telemetries
                    .OfType<TraceTelemetry>()
                    .OrderBy(t => t.Message)
                    .ToArray();

                string expectedFunctionCategory = LogCategories.CreateFunctionCategory(testName);
                string expectedFunctionUserCategory = LogCategories.CreateFunctionUserCategory(testName);
                string expectedOperationId = request.Context.Operation.Id;

                ValidateTrace(telemetries[0], "ApplicationInsightsLoggerOptions", "Microsoft.Azure.WebJobs.Hosting.OptionsLoggingService");
                ValidateTrace(telemetries[1], "Executed ", expectedFunctionCategory, testName, invocationId, expectedOperationId, request.Id);
                ValidateTrace(telemetries[2], "Executing ", expectedFunctionCategory, testName, invocationId, expectedOperationId, request.Id);
                ValidateTrace(telemetries[3], "Found the following functions:\r\n", LogCategories.Startup);
                ValidateTrace(telemetries[4], "FunctionResultAggregatorOptions", "Microsoft.Azure.WebJobs.Hosting.OptionsLoggingService");
                ValidateTrace(telemetries[5], "Job host started", LogCategories.Startup);
                ValidateTrace(telemetries[6], "Job host stopped", LogCategories.Startup);
                ValidateTrace(telemetries[7], "Logger", expectedFunctionUserCategory, testName, invocationId, expectedOperationId, request.Id, hasCustomScope: true);
                ValidateTrace(telemetries[8], "LoggerFilterOptions", "Microsoft.Azure.WebJobs.Hosting.OptionsLoggingService");
                ValidateTrace(telemetries[9], "LoggerFilterOptions", "Microsoft.Azure.WebJobs.Hosting.OptionsLoggingService");
                ValidateTrace(telemetries[10], "SingletonOptions", "Microsoft.Azure.WebJobs.Hosting.OptionsLoggingService");
                ValidateTrace(telemetries[11], "Starting JobHost", "Microsoft.Azure.WebJobs.Hosting.JobHostService");
                ValidateTrace(telemetries[12], "Stopping JobHost", "Microsoft.Azure.WebJobs.Hosting.JobHostService");
                ValidateTrace(telemetries[13], "Trace", expectedFunctionUserCategory, testName, invocationId, expectedOperationId, request.Id);

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
                MethodInfo methodInfo = GetType().GetMethod(testName, BindingFlags.Public | BindingFlags.Static);
                await Assert.ThrowsAsync<FunctionInvocationException>(() => host.GetJobHost().CallAsync(methodInfo, new { input = "function input" }));
                await host.StopAsync();

                Assert.Equal(19, _channel.Telemetries.Count);

                // Validate the request
                RequestTelemetry request = _channel.Telemetries
                    .OfType<RequestTelemetry>()
                    .Single();
                ValidateRequest(request, testName, testName, null, null, false);

                // invocation id is retrievable from the request
                request.Properties.TryGetValue(LogConstants.InvocationIdKey, out string invocationId);

                // Validate the traces. Order by message string as the requests may come in
                // slightly out-of-order or on different threads
                TraceTelemetry[] telemetries = _channel.Telemetries
                    .OfType<TraceTelemetry>()
                    .OrderBy(t => t.Message)
                    .ToArray();

                string expectedFunctionCategory = LogCategories.CreateFunctionCategory(testName);
                string expectedFunctionUserCategory = LogCategories.CreateFunctionUserCategory(testName);
                string expectedOperationId = request.Context.Operation.Id;

                ValidateTrace(telemetries[0], "ApplicationInsightsLoggerOptions", "Microsoft.Azure.WebJobs.Hosting.OptionsLoggingService");
                ValidateTrace(telemetries[1], "Error", expectedFunctionUserCategory, testName, invocationId, expectedOperationId, request.Id, expectedLogLevel: LogLevel.Error);
                ValidateTrace(telemetries[2], "Executed", expectedFunctionCategory, testName, invocationId, expectedOperationId, request.Id, expectedLogLevel: LogLevel.Error);
                ValidateTrace(telemetries[3], "Executing", expectedFunctionCategory, testName, invocationId, expectedOperationId, request.Id);
                ValidateTrace(telemetries[4], "Found the following functions:\r\n", LogCategories.Startup);
                ValidateTrace(telemetries[5], "FunctionResultAggregatorOptions", "Microsoft.Azure.WebJobs.Hosting.OptionsLoggingService");
                ValidateTrace(telemetries[6], "Job host started", LogCategories.Startup);
                ValidateTrace(telemetries[7], "Job host stopped", LogCategories.Startup);
                ValidateTrace(telemetries[8], "Logger", expectedFunctionUserCategory, testName, invocationId, expectedOperationId, request.Id, hasCustomScope: true);
                ValidateTrace(telemetries[9], "LoggerFilterOptions", "Microsoft.Azure.WebJobs.Hosting.OptionsLoggingService");
                ValidateTrace(telemetries[10], "LoggerFilterOptions", "Microsoft.Azure.WebJobs.Hosting.OptionsLoggingService");
                ValidateTrace(telemetries[11], "SingletonOptions", "Microsoft.Azure.WebJobs.Hosting.OptionsLoggingService");
                ValidateTrace(telemetries[12], "Starting JobHost", "Microsoft.Azure.WebJobs.Hosting.JobHostService");
                ValidateTrace(telemetries[13], "Stopping JobHost", "Microsoft.Azure.WebJobs.Hosting.JobHostService");
                ValidateTrace(telemetries[14], "Trace", expectedFunctionUserCategory, testName, invocationId, expectedOperationId, request.Id);

                // Validate the exception
                ExceptionTelemetry[] exceptions = _channel.Telemetries
                    .OfType<ExceptionTelemetry>()
                    .OrderBy(t => t.Timestamp)
                    .ToArray();
                Assert.Equal(3, exceptions.Length);
                ValidateException(exceptions[0], expectedFunctionUserCategory, testName, expectedOperationId, request.Id);
                ValidateException(exceptions[1], expectedFunctionCategory, testName, expectedOperationId, request.Id);
                ValidateException(exceptions[2], LogCategories.Results, testName, expectedOperationId, request.Id);
            }
        }

        [Theory]
        [InlineData(LogLevel.None, 0, 0)]
        [InlineData(LogLevel.Information, 5, 10)] // 5 start, 2 stop, 4x traces per request, 1x requests
        [InlineData(LogLevel.Warning, 2, 0)] // 2x warning trace per request
        public async Task QuickPulse_Works_EvenIfFiltered(LogLevel defaultLevel, int tracesPerRequest, int additionalTraces)
        {
            using (QuickPulseEventListener qpListener = new QuickPulseEventListener())
            using (IHost host = ConfigureHost(defaultLevel))
            {
                ApplicationInsightsTestListener listener = new ApplicationInsightsTestListener();
                int functionsCalled = 0;
                bool keepRunning = true;
                Task functionTask = null;

                try
                {
                    listener.StartListening();

                    JobHost jobHost = host.GetJobHost();
                    {
                        await host.StartAsync();

                        await TestHelpers.Await(() => listener.IsReady);

                        MethodInfo methodInfo = GetType().GetMethod(nameof(TestApplicationInsightsWarning), BindingFlags.Public | BindingFlags.Static);

                        // Start a task to make calls in a loop.
                        functionTask = Task.Run(async () =>
                        {
                            while (keepRunning)
                            {
                                await Task.Delay(100);
                                await jobHost.CallAsync(methodInfo);
                                functionsCalled++;
                            }
                        });

                        // Wait until we're seeing telemetry come through the QuickPulse service
                        double? sum = null;
                        await TestHelpers.Await(() =>
                        {
                            // Sum up all req/sec calls that we've received.
                            IEnumerable<QuickPulseMetric> reqPerSec = listener.GetQuickPulseItems()
                               .Select(p => p.Metrics.Where(q => q.Name == @"\ApplicationInsights\Requests/Sec").Single());
                            sum = reqPerSec.Sum(p => p.Value);

                            // All requests will go to QuickPulse.
                            // Just make sure we have some coming through. Choosing 5 as an arbitrary number.
                            return sum >= 5;
                        }, timeout: 5000,
                        userMessageCallback: () =>
                        {
                            IEnumerable<QuickPulsePayload> items = listener.GetQuickPulseItems().OrderBy(i => i.Timestamp).Take(10);
                            IEnumerable<string> s = items.Select(i => $"[{i.Timestamp.ToString(_dateFormat)}] {i.Metrics.Single(p => p.Name == @"\ApplicationInsights\Requests/Sec")}");
                            return $"Actual QuickPulse sum: '{sum}'. DefaultLevel: '{defaultLevel}'.{Environment.NewLine}QuickPulse items ({items.Count()}): {string.Join(Environment.NewLine, s)}{Environment.NewLine}QuickPulse Logs:{qpListener.GetLog(20)}{Environment.NewLine}Logs: {host.GetTestLoggerProvider().GetLogString()}";
                        });
                    }
                }
                finally
                {
                    keepRunning = false;
                    await functionTask;
                    await host.StopAsync();
                    listener.StopListening();
                    listener.Dispose();
                }

                string GetFailureString()
                {
                    return string.Join(Environment.NewLine, _channel.Telemetries.OrderBy(p => p.Timestamp).Select(t =>
                      {
                          string timestamp = $"[{t.Timestamp.ToString(_dateFormat)}] ";
                          switch (t)
                          {
                              case DependencyTelemetry dependency:
                                  return timestamp + $"[Dependency] {dependency.Name}; {dependency.Target}; {dependency.Data}";
                              case TraceTelemetry trace:
                                  return timestamp + $"[Trace] {trace.Message}";
                              case RequestTelemetry request:
                                  return timestamp + $"[Request] {request.Name}: {request.Success}";
                              default:
                                  return timestamp + $"[{t.GetType().Name}]";
                          }
                      }));
                }

                int expectedTelemetryItems = additionalTraces + (functionsCalled * tracesPerRequest);

                // Filter out the odd auto-tracked request that we occassionally see from AppVeyor. 
                // From here: https://github.com/xunit/xunit/blob/9d10262a3694bb099ddd783d735aba2a7aac759d/src/xunit.runner.reporters/AppVeyorClient.cs#L21
                var actualTelemetries = _channel.Telemetries
                    .Where(p => !(p is DependencyTelemetry d && d.Name == "POST /api/tests/batch"))
                    .ToArray();

                Assert.True(actualTelemetries.Length == expectedTelemetryItems,
                    $"Expected: {expectedTelemetryItems}; Actual: {actualTelemetries.Length}{Environment.NewLine}{GetFailureString()}");
            }
        }

        [Fact]
        public async Task ApplicationInsights_AIUsedExplicitlyFromFunctionCode()
        {
            string testName = nameof(TestApplicationInsightsExplicitCall);
            using (IHost host = ConfigureHost())
            {
                await host.StartAsync();
                MethodInfo methodInfo = GetType().GetMethod(testName, BindingFlags.Public | BindingFlags.Static);
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
        public async Task ApplicationInsights_OuterRequestIsTrackedOnce()
        {
            string testName = nameof(TestApplicationInsightsInformation);
            using (IHost host = ConfigureHost())
            {
                TelemetryClient telemetryClient = host.Services.GetService<TelemetryClient>();
                await host.StartAsync();
                MethodInfo methodInfo = GetType().GetMethod(testName, BindingFlags.Public | BindingFlags.Static);

                RequestTelemetry outerRequest = null;

                // simulate auto tracked HTTP incoming call
                using (IOperationHolder<RequestTelemetry> operation = telemetryClient.StartOperation<RequestTelemetry>("request name"))
                {
                    outerRequest = operation.Telemetry;
                    outerRequest.Url = new Uri("http://my-func/api/func-name?name=123");
                    outerRequest.Success = true;
                    await host.GetJobHost().CallAsync(methodInfo, new { input = "input" });
                }

                await host.StopAsync();

                // Validate the request
                // There must be only one reported by the AppInsights request collector
                RequestTelemetry[] requestTelemetries = _channel.Telemetries.OfType<RequestTelemetry>().ToArray();
                Assert.Single(requestTelemetries);

                RequestTelemetry functionRequest = requestTelemetries.Single();
                Assert.Same(outerRequest, functionRequest);

                Assert.True(double.TryParse(functionRequest.Properties[LogConstants.FunctionExecutionTimeKey], out double functionDuration));
                Assert.True(functionRequest.Duration.TotalMilliseconds >= functionDuration);
                Assert.Equal("0.0.0.0", functionRequest.Context.Location.Ip);
                ValidateRequest(functionRequest, testName, testName, "GET", "/api/func-name", true, "0");
            }
        }

        [Theory]
        [InlineData(nameof(TestApplicationInsightsInformation), true)]
        [InlineData(nameof(TestApplicationInsightsFailure), false)]
        public async Task ApplicationInsights_HttpRequestTrackingByWebJobs(string testName, bool success)
        {
            var client = _factory.CreateClient();
            var httpOptions = new HttpAutoCollectionOptions
            {
                EnableHttpTriggerExtendedInfoCollection = false
            };

            using (IHost host = ConfigureHost(httpOptions: httpOptions))
            {
                Startup.Host = host;
                await host.StartAsync();

                var loggerProvider = host.Services.GetServices<ILoggerProvider>().OfType<ApplicationInsightsLoggerProvider>().Single();
                var logger = loggerProvider.CreateLogger(LogCategories.Results);

                var request = new HttpRequestMessage(HttpMethod.Get, $"/some/path?name={testName}");
                request.Headers.Add("traceparent", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

                var mockHttpContext = new DefaultHttpContext();
                mockHttpContext.Connection.RemoteIpAddress = new IPAddress(new byte[] { 1, 2, 3, 4 });

                // simulate functions behavior to set request on the scope
                using (var _ = logger.BeginScope(new Dictionary<string, object> { ["MS_HttpRequest"] = mockHttpContext.Request }))
                {
                    await client.SendAsync(request);
                }

                await host.StopAsync();

                // Validate the request
                // There must be only one reported by the AppInsights request collector
                // The telemetry may come back slightly later, so wait until we see it
                RequestTelemetry functionRequest = null;
                await TestHelpers.Await(() =>
                {
                    functionRequest = _channel.Telemetries.OfType<RequestTelemetry>().SingleOrDefault();
                    return functionRequest != null;
                });

                Assert.True(double.TryParse(functionRequest.Properties[LogConstants.FunctionExecutionTimeKey], out double functionDuration));
                Assert.True(functionRequest.Duration.TotalMilliseconds >= functionDuration);
                Assert.Equal("1.2.3.4", functionRequest.Context.Location.Ip);
                Assert.Null(functionRequest.Url);

                ValidateRequest(
                    functionRequest,
                    testName,
                    testName,
                    null,
                    null,
                    success);

                // Make sure operation ids match
                var traces = _channel.Telemetries.OfType<TraceTelemetry>()
                    .Where(t => t.Context.Operation.Id == functionRequest.Context.Operation.Id);
                Assert.Equal(success ? 4 : 5, traces.Count());
            }
        }

        [Theory]
        [InlineData(nameof(TestApplicationInsightsInformation), true)]
        [InlineData(nameof(TestApplicationInsightsFailure), false)]
        public async Task ApplicationInsights_HttpRequestTrackingByAIAutoCollector(string testName, bool success)
        {
            var client = _factory.CreateClient();

            using (IHost host = ConfigureHost())
            {
                Startup.Host = host;
                await host.StartAsync();

                var loggerProvider = host.Services.GetServices<ILoggerProvider>().OfType<ApplicationInsightsLoggerProvider>().Single();
                var logger = loggerProvider.CreateLogger(LogCategories.Results);

                var request = new HttpRequestMessage(HttpMethod.Get, $"/some/path?name={testName}");
                request.Headers.Add("traceparent", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

                var mockHttpContext = new DefaultHttpContext();
                mockHttpContext.Connection.RemoteIpAddress = new IPAddress(new byte[] {1, 2, 3, 4});

                // simulate functions behavior to set request on the scope
                using (var _ = logger.BeginScope(new Dictionary<string, object> { ["MS_HttpRequest"] = mockHttpContext.Request}))
                {
                    await client.SendAsync(request);
                }

                await host.StopAsync();

                // Validate the request
                // There must be only one reported by the AppInsights request collector
                // The telemetry may come back slightly later, so wait until we see it
                RequestTelemetry functionRequest = null;
                await TestHelpers.Await(() =>
                {
                    functionRequest = _channel.Telemetries.OfType<RequestTelemetry>().SingleOrDefault();
                    return functionRequest != null;
                });

                Assert.True(double.TryParse(functionRequest.Properties[LogConstants.FunctionExecutionTimeKey], out double functionDuration));
                Assert.True(functionRequest.Duration.TotalMilliseconds >= functionDuration);
                Assert.Equal("1.2.3.4", functionRequest.Context.Location.Ip);
                Assert.Equal("http://localhost/some/path", functionRequest.Url.ToString());
                
                ValidateRequest(
                    functionRequest,
                    testName,
                    testName,
                    "GET",
                    "/some/path",
                    success,
                    "204",
                    "4bf92f3577b34da6a3ce929d0e0e4736",
                    "|4bf92f3577b34da6a3ce929d0e0e4736.00f067aa0ba902b7.");

                Assert.DoesNotContain("MS_HttpRequest", functionRequest.Properties.Keys);
                // Make sure operation ids match
                var traces = _channel.Telemetries.OfType<TraceTelemetry>()
                    .Where(t => t.Context.Operation.Id == functionRequest.Context.Operation.Id);
                Assert.Equal(success ? 4 : 5, traces.Count());
            }
        }

        [Fact]
        public async Task ApplicationInsights_HttpRequestTrackingByWebJobsFirstRequest()
        {
            var client = _factory.CreateClient();
            var httpOptions = new HttpAutoCollectionOptions
            {
                EnableHttpTriggerExtendedInfoCollection = false
            };

            // simulate functions workaround to track first cold requests
            _requestModuleForFirstRequest.Initialize(null);

            using (IHost host = ConfigureHost(httpOptions: httpOptions))
            {
                Startup.Host = host;
                await host.StartAsync();

                var request = new HttpRequestMessage(HttpMethod.Get, $"/some/path?name={nameof(TestApplicationInsightsDisposeRequestsModule)}");

                await client.SendAsync(request);

                await host.StopAsync();

                // Validate the request
                // There must be only one reported by the AppInsights request collector
                // The telemetry may come back slightly later, so wait until we see it
                RequestTelemetry functionRequest = null;
                await TestHelpers.Await(() =>
                {
                    functionRequest = _channel.Telemetries.OfType<RequestTelemetry>().SingleOrDefault();
                    return functionRequest != null;
                });

                Assert.True(double.TryParse(functionRequest.Properties[LogConstants.FunctionExecutionTimeKey], out double functionDuration));
                Assert.True(functionRequest.Duration.TotalMilliseconds >= functionDuration);
                Assert.Null(functionRequest.Url);

                ValidateRequest(
                    functionRequest,
                    nameof(TestApplicationInsightsDisposeRequestsModule),
                    nameof(TestApplicationInsightsDisposeRequestsModule),
                    null,
                    null,
                    true);

                // Make sure operation ids match
                var traces = _channel.Telemetries.OfType<TraceTelemetry>()
                    .Where(t => t.Context.Operation.Id == functionRequest.Context.Operation.Id);
                Assert.Equal(2, traces.Count());
            }
        }

        [Fact]
        public async Task ApplicationInsights_HttpRequestTrackingByAIAutoCollectorFirstRequest()
        {
            var client = _factory.CreateClient();

            // simulate functions workaround to track first cold requests
            _requestModuleForFirstRequest.Initialize(null);

            using (IHost host = ConfigureHost())
            {
                Startup.Host = host;
                await host.StartAsync();

                var request = new HttpRequestMessage(HttpMethod.Get, $"/some/path?name={nameof(TestApplicationInsightsDisposeRequestsModule)}");

                await client.SendAsync(request);

                await host.StopAsync();

                // Validate the request
                // There must be only one reported by the AppInsights request collector
                // The telemetry may come back slightly later, so wait until we see it
                RequestTelemetry functionRequest = null;
                await TestHelpers.Await(() =>
                {
                    functionRequest = _channel.Telemetries.OfType<RequestTelemetry>().SingleOrDefault();
                    return functionRequest != null;
                });

                Assert.True(double.TryParse(functionRequest.Properties[LogConstants.FunctionExecutionTimeKey], out double functionDuration));
                Assert.True(functionRequest.Duration.TotalMilliseconds >= functionDuration);
                Assert.Equal("http://localhost/some/path", functionRequest.Url.ToString());

                ValidateRequest(
                    functionRequest,
                    nameof(TestApplicationInsightsDisposeRequestsModule),
                    nameof(TestApplicationInsightsDisposeRequestsModule),
                    "GET",
                    "/some/path",
                    true,
                    "204");

                // Make sure operation ids match
                var traces = _channel.Telemetries.OfType<TraceTelemetry>()
                    .Where(t => t.Context.Operation.Id == functionRequest.Context.Operation.Id);
                Assert.Equal(2, traces.Count());
            }
        }

        [Fact]
        public async Task ApplicationInsights_HttpRequestTracking_IgnoresDuplicateRequests()
        {
            // During Functions host shutdown/restart events, it's possible to have two 
            // simultaneous running hosts for a very short period. We need to make sure we don't
            // double-log any of the auto-tracked Requests.

            var client = _factory.CreateClient();

            // Create two hosts to simulate.
            using (IHost host1 = ConfigureHost())
            {
                using (IHost host2 = ConfigureHost())
                {
                    Startup.Host = host2;
                    await host1.StartAsync();
                    await host2.StartAsync();

                    string testName = nameof(TestApplicationInsightsInformation);

                    var request = new HttpRequestMessage(HttpMethod.Get, $"/some/path?name={testName}");
                    request.Headers.Add("traceparent", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

                    await client.SendAsync(request);

                    await host1.StopAsync();
                    await host2.StopAsync();

                    // Validate the request
                    // There must be only one reported by the AppInsights request collector
                    // The telemetry may come back slightly later, so wait until we see it
                    RequestTelemetry functionRequest = null;
                    await TestHelpers.Await(() =>
                    {
                        functionRequest = _channel.Telemetries.OfType<RequestTelemetry>().SingleOrDefault();
                        return functionRequest != null;
                    });

                    Assert.True(double.TryParse(functionRequest.Properties[LogConstants.FunctionExecutionTimeKey], out double functionDuration));
                    Assert.True(functionRequest.Duration.TotalMilliseconds >= functionDuration);
                    ValidateRequest(
                        functionRequest,
                        testName,
                        testName,
                        "GET",
                        "/some/path",
                        true,
                        "204",
                        "4bf92f3577b34da6a3ce929d0e0e4736",
                        "|4bf92f3577b34da6a3ce929d0e0e4736.00f067aa0ba902b7.");

                    Assert.Equal(_expectedResponseCode.ToString(), functionRequest.ResponseCode);
                }
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
            TelemetryClient telemetryClient = new TelemetryClient(); // use TelemetryConfiguration.Active
            telemetryClient.TrackEvent("custom event");
        }
        
        [NoAutomaticTrigger]
        public static void TestApplicationInsightsDisposeRequestsModule(string input, ILogger logger)
        {
            _requestModuleForFirstRequest.Dispose();
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

            ValidateSdkVersion(telemetry, "af_");
        }

        private static void ValidateCustomScopeProperty(ISupportProperties telemetry)
        {
            Assert.Equal(_customScopeValue, telemetry.Properties[$"{LogConstants.CustomPropertyPrefix}{_customScopeKey}"]);
        }

        private class ApplicationInsightsTestListener : IDisposable
        {
            private readonly HttpListener _applicationInsightsListener = new HttpListener();
            private Task _listenTask;
            private int _posts;

            private readonly ConcurrentQueue<QuickPulsePayload> _quickPulseItems = new ConcurrentQueue<QuickPulsePayload>();

            private CancellationTokenSource _tcs;

            public IEnumerable<QuickPulsePayload> GetQuickPulseItems()
            {
                return _quickPulseItems.ToList();
            }

            // Make sure collection has started.
            public bool IsReady => _posts >= 2;

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
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

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
                    foreach (QuickPulsePayload i in quickPulse)
                    {
                        _quickPulseItems.Enqueue(i);
                    }
                    _posts++;
                }
            }

            private static string GetRequestContent(HttpListenerRequest request)
            {
                string result = null;
                if (request.HasEntityBody)
                {
                    using (Stream requestInputStream = request.InputStream)
                    {
                        Encoding encoding = request.ContentEncoding;
                        using (StreamReader reader = new StreamReader(requestInputStream, encoding))
                        {
                            result = reader.ReadToEnd();
                        }
                    }
                }
                return result;
            }

            private static string Decompress(string content)
            {
                byte[] zippedData = Encoding.Default.GetBytes(content);
                using (MemoryStream ms = new MemoryStream(zippedData))
                {
                    using (GZipStream compressedzipStream = new GZipStream(ms, CompressionMode.Decompress))
                    {
                        MemoryStream outputStream = new MemoryStream();
                        byte[] block = new byte[1024];
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
                _applicationInsightsListener.Close();
                (_applicationInsightsListener as IDisposable)?.Dispose();
            }
        }

        private static void ValidateTrace(TraceTelemetry telemetry, string expectedMessageStartsWith, string expectedCategory,
            string expectedOperationName = null, string expectedInvocationId = null, string expectedOperationId = null,
            string expectedParentId = null, bool hasCustomScope = false, LogLevel expectedLogLevel = LogLevel.Information)
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

                Assert.True(telemetry.Properties.TryGetValue(LogConstants.InvocationIdKey, out string id));
                Assert.Equal(expectedInvocationId, id);
            }
            else
            {
                Assert.Null(telemetry.Context.Operation.Name);
                Assert.Null(telemetry.Context.Operation.Id);
                Assert.Null(telemetry.Context.Operation.ParentId);
                Assert.False(telemetry.Properties.TryGetValue(LogConstants.InvocationIdKey, out string unused));
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

        private static void ValidateRequest(RequestTelemetry telemetry, string operationName, string name, string httpMethod, string requestPath, bool success, string statusCode = "0",
            string operationId = null, string parentId = null)
        {
            Assert.NotNull(telemetry.Context.Operation.Id);
            Assert.Equal(operationName, telemetry.Context.Operation.Name);
            Assert.NotNull(telemetry.Duration);
            Assert.Equal(success, telemetry.Success);

            Assert.NotNull(telemetry.Properties[LogConstants.InvocationIdKey]);

            Assert.Equal($"ApplicationInsightsEndToEndTests.{operationName}", telemetry.Properties[LogConstants.FullNameKey]);
            Assert.Equal("This function was programmatically called via the host APIs.", telemetry.Properties[LogConstants.TriggerReasonKey]);

            TelemetryValidationHelpers.ValidateRequest(telemetry, operationName, name, operationId, parentId, LogCategories.Results,
                success ? LogLevel.Information : LogLevel.Error, success, statusCode);
        }

        private static void ValidateSdkVersion(ITelemetry telemetry, string prefix = null)
        {            
            Assert.StartsWith($"{prefix}webjobs:", telemetry.Context.GetInternalContext().SdkVersion);
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
            private readonly IList<string> _logs = new List<string>();

            public string GetLog(int lines)
            {
                return string.Join(Environment.NewLine, _logs.Take(lines));
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                List<object> trimmedData = eventData.Payload.ToList();
                trimmedData.RemoveAt(trimmedData.Count - 1);

                string log = string.Format(eventData.Message, trimmedData.ToArray());

                _logs.Add($"[{DateTime.UtcNow.ToString(_dateFormat)}] {log}");

                base.OnEventWritten(eventData);
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

        public void Dispose()
        {
            _channel?.Dispose();

            while (Activity.Current != null)
            {
                Activity.Current.Stop();
            }
        }


        public class Startup
        {
            public static IHost Host;

            public void ConfigureServices(IServiceCollection services)
            {
            }

            public void Configure(IApplicationBuilder app, AspNetCore.Hosting.IHostingEnvironment env)
            {
                app.Run(async (context) =>
                {
                    MethodInfo methodInfo = typeof(ApplicationInsightsEndToEndTests).GetMethod(context.Request.Query["name"], BindingFlags.Public | BindingFlags.Static);

                    try
                    {
                        await Host.GetJobHost().CallAsync(methodInfo, new {input = "input"});
                    }
                    catch
                    {
                        // Ignore this, it shouldn't matter what is returned as we'll log the
                        // result of the function invocation no matter what.
                    }

                    context.Response.StatusCode = _expectedResponseCode;
                    await context.Response.WriteAsync("Hello World!");
                });
            }
        }

        public class CustomTestWebHostFactory : WebApplicationFactory<ApplicationInsightsEndToEndTests.Startup>
        {
            protected override IWebHostBuilder CreateWebHostBuilder()
            {
                return WebHost.CreateDefaultBuilder()
                    .UseStartup<ApplicationInsightsEndToEndTests.Startup>();
            }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseContentRoot(".");
                base.ConfigureWebHost(builder);
            }
        }
    }
}