using System;
using System.Collections.Generic;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
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

namespace Benchmarks
{
    [MemoryDiagnoser]
    public class WebJobsLoggingApplicationInsights
    {
        private readonly Guid _invocationId = Guid.NewGuid();
        private readonly Guid _hostInstanceId = Guid.NewGuid();
        private readonly DateTime _startTime = DateTime.UtcNow;
        private readonly DateTime _endTime;
        private const string TriggerReason = "new queue message";
        private const string FunctionFullName = "Functions.TestFunction";
        private const string FunctionShortName = "TestFunction";
        private readonly IDictionary<string, string> _arguments;
        private readonly NoopTelemetryChannel _channel = new NoopTelemetryChannel();
        private readonly TelemetryClient _client;
        private readonly int _durationMs = 450;
        private readonly IHost _host;
        private readonly ILogger _logger;
        private readonly FunctionInstanceLogEntry _exceptionResult;
        private readonly IFunctionInstance _functionInstance;

        public WebJobsLoggingApplicationInsights()
        {
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
                FullName = FunctionFullName,
                ShortName = FunctionShortName
            };

            _exceptionResult = CreateDefaultInstanceLogEntry(new FunctionInvocationException("Failed"));
            _logger = CreateLogger(LogCategories.Results);
            _functionInstance = CreateFunctionInstance(_invocationId);
        }

        [Benchmark]
        public void OnException()
        {
            using (_logger.BeginFunctionScope(_functionInstance, _hostInstanceId))
            {
                _logger.LogFunctionResult(_exceptionResult);
            }
        }

        [Benchmark]
        public void MultipleLogs()
        {
            using (_logger.BeginFunctionScope(_functionInstance, _hostInstanceId))
            {
                _logger.LogInformation("Using {some} custom {properties}. {Test}.", "1", 2, "3");
                _logger.LogInformation("Simple log statement");
                _logger.LogInformation(new EventId(1),"With event id");
            }
        }

        private FunctionInstanceLogEntry CreateDefaultInstanceLogEntry(Exception ex = null)
        {
            return new FunctionInstanceLogEntry
            {
                FunctionName = FunctionFullName,
                LogName = FunctionShortName,
                FunctionInstanceId = _invocationId,
                StartTime = _startTime,
                EndTime = _endTime,
                LogOutput = "a bunch of output that we will not forward", // not used here -- this is all Traced
                TriggerReason = TriggerReason,
                ParentId = Guid.NewGuid(), // we do not track this
                ErrorDetails = null, // we do not use this -- we pass the exception in separately
                Arguments = _arguments,
                Duration = TimeSpan.FromMilliseconds(_durationMs),
                Exception = ex
            };
        }

        private ILogger CreateLogger(string category)
        {
            return new ApplicationInsightsLogger(_client, category, new ApplicationInsightsLoggerOptions());
        }

        private IFunctionInstance CreateFunctionInstance(Guid id, Dictionary<string, string> triggerDetails = null)
        {
            var method = typeof(WebJobsLoggingApplicationInsights).GetMethod(nameof(TestFunction), BindingFlags.NonPublic | BindingFlags.Static);
            var descriptor = FunctionIndexer.FromMethod(method, new ConfigurationBuilder().Build());

            return new FunctionInstance(id, triggerDetails ?? new Dictionary<string, string>(), null, new ExecutionReason(), null, null, descriptor, null);
        }

        [FunctionName(nameof(TestFunction))]
        private static void TestFunction()
        {
            // used for a FunctionDescriptor
        }

        class NoopTelemetryChannel : ITelemetryChannel
        {
            public bool? DeveloperMode { get; set; }
            public string EndpointAddress { get; set; }
            public void Dispose() { }
            public void Flush() { }
            public void Send(ITelemetry item) { }
        }
    }
}