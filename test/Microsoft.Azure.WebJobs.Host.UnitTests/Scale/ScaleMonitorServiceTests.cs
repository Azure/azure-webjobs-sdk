// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Scale
{
    [Trait(TestTraits.CategoryTraitName, TestTraits.ScaleMonitoring)]
    public class ScaleMonitorServiceTests
    {
        private readonly ScaleMonitorService _monitor;
        private readonly TestMetricsRepository _metricsRepository;
        private readonly TestLoggerProvider _loggerProvider;
        private readonly PrimaryHostStateProvider _primaryHostStateProvider;
        private readonly Mock<IScaleMonitorManager> _monitorManagerMock;
        private readonly Mock<ITargetScalerManager> _targetScalerManagerMock;
        private readonly IConfiguration _configuration;
        private List<IScaleMonitor> _monitors;
        private List<ITargetScaler> _scalers;

        public ScaleMonitorServiceTests()
        {
            _monitors = new List<IScaleMonitor>();
            _scalers = new List<ITargetScaler>();

            _metricsRepository = new TestMetricsRepository();
            _loggerProvider = new TestLoggerProvider();
            ILoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(_loggerProvider);

            Mock<ScaleManager> functionsScaleManagerMock = new Mock<ScaleManager>();
            _monitorManagerMock = new Mock<IScaleMonitorManager>(MockBehavior.Strict);
            _monitorManagerMock.Setup(p => p.GetMonitors()).Returns(() => _monitors);
            _targetScalerManagerMock = new Mock<ITargetScalerManager>(MockBehavior.Strict);
            _targetScalerManagerMock.Setup(p => p.GetTargetScalers()).Returns(() => _scalers);
            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string> { { $"Microsoft.Azure.WebJobs.Host.UnitTests", "1" } }).Build();

            IOptions<ScaleOptions> options = Options.Create(new ScaleOptions()
            {
                ScaleMetricsSampleInterval = TimeSpan.FromSeconds(1),
                IsRuntimeScalingEnabled = true
            });

            _primaryHostStateProvider = new PrimaryHostStateProvider() { IsPrimary = true };

            _monitor = new ScaleMonitorService(functionsScaleManagerMock.Object,
                _metricsRepository,
                options,
                _primaryHostStateProvider,
                _monitorManagerMock.Object,
                _targetScalerManagerMock.Object,
                _configuration,
                loggerFactory);
        }

        [Fact]
        public async Task OnTimer_ExceptionsAreHandled()
        {
            var monitor = new TestScaleMonitor1
            {
                Exception = new Exception("Kaboom!")
            };
            _monitors.Add(monitor);

            await _monitor.StartAsync(CancellationToken.None);

            // wait for a few failures to happen
            LogMessage[] logs = null;
            await TestHelpers.Await(() =>
            {
                logs = _loggerProvider.GetAllLogMessages().Where(p => p.Level == LogLevel.Error).ToArray();
                return logs.Length >= 3;
            });

            Assert.All(logs,
                p =>
                {
                    Assert.Same(monitor.Exception, p.Exception);
                    Assert.Equal("Function 'testscalemonitor1' error: Failed to collect scale metrics.", p.FormattedMessage);
                });
        }

        [Fact]
        public async Task OnTimer_DoesNotSample_WhenNotPrimaryHost()
        {
            _primaryHostStateProvider.IsPrimary = false;

            var monitor = new TestScaleMonitor1();
            _monitors.Add(monitor);

            await _monitor.StartAsync(CancellationToken.None);

            await Task.Delay(100);

            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            Assert.Single(logs);
            Assert.StartsWith("Runtime scale monitoring is enabled.", logs[0].FormattedMessage);
        }

        [Fact]
        public async Task OnTimer_Sample_WhenPrimaryHost()
        {
            var monitor = new TestScaleMonitor1();
            _monitors.Add(monitor);

            await _monitor.StartAsync(CancellationToken.None);

            await Task.Delay(2000);

            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            Assert.StartsWith("Runtime scale monitoring is enabled.", logs[0].FormattedMessage);
            Assert.Equal($"Taking metrics samples for 1 monitor(s).", logs[1].FormattedMessage);
        }

        [Fact]
        public async Task OnTimer_PersistsMetrics()
        {
            var testMetrics = new List<TestScaleMetrics1>
            {
                new TestScaleMetrics1 { Count = 10 },
                new TestScaleMetrics1 { Count = 15 },
                new TestScaleMetrics1 { Count = 45 },
                new TestScaleMetrics1 { Count = 50 },
                new TestScaleMetrics1 { Count = 100 }
            };
            var monitor1 = new TestScaleMonitor1
            {
                Metrics = testMetrics
            };
            _monitors.Add(monitor1);

            await _monitor.StartAsync(CancellationToken.None);

            await TestHelpers.Await(() =>
            {
                return _metricsRepository.Count >= 5;
            });

            var logs = _loggerProvider.GetAllLogMessages().ToArray();

            var infoLogs = logs.Where(p => p.Level == LogLevel.Information);
            Assert.StartsWith("Runtime scale monitoring is enabled.", logs[0].FormattedMessage);
            Assert.Equal("Taking metrics samples for 1 monitor(s).", logs[1].FormattedMessage);
            Assert.True(logs[2].FormattedMessage.StartsWith("Scale metrics sample for monitor 'testscalemonitor1': {\"Count\":10,"));

            var metricsWritten = _metricsRepository.Metrics[monitor1].Take(5);
            Assert.Equal(testMetrics, metricsWritten);
        }

        [Fact]
        public async Task OnTimer_MonitorFailuresAreHandled()
        {
            var testMetrics1 = new List<TestScaleMetrics1>
            {
                new TestScaleMetrics1 { Count = 10 },
                new TestScaleMetrics1 { Count = 15 },
                new TestScaleMetrics1 { Count = 45 },
                new TestScaleMetrics1 { Count = 50 },
                new TestScaleMetrics1 { Count = 100 }
            };
            var monitor1 = new TestScaleMonitor1
            {
                Exception = new Exception("Kaboom!")
            };
            _monitors.Add(monitor1);

            var testMetrics2 = new List<TestScaleMetrics2>
            {
                new TestScaleMetrics2 { Num = 300 },
                new TestScaleMetrics2 { Num = 350 },
                new TestScaleMetrics2 { Num = 400 },
                new TestScaleMetrics2 { Num = 450 },
                new TestScaleMetrics2 { Num = 500 }
            };
            var monitor2 = new TestScaleMonitor2
            {
                Metrics = testMetrics2
            };
            _monitors.Add(monitor2);

            await _monitor.StartAsync(CancellationToken.None);

            await TestHelpers.Await(() =>
            {
                return _metricsRepository.Count >= 5;
            });

            var logs = _loggerProvider.GetAllLogMessages().ToArray();

            var infoLogs = logs.Where(p => p.Level == LogLevel.Information);
            Assert.StartsWith("Runtime scale monitoring is enabled.", logs[0].FormattedMessage);
            Assert.Equal("Taking metrics samples for 2 monitor(s).", logs[1].FormattedMessage);

            // verify the failure logs for the failing monitor
            Assert.True(logs.Count(p => p.FormattedMessage.Equals($"Function 'testscalemonitor1' error: Failed to collect scale metrics.")) >= 5);

            // verify each successful sample is logged
            Assert.True(logs.Count(p => p.FormattedMessage.StartsWith($"Scale metrics sample for monitor 'testscalemonitor2'")) >= 5);

            var metricsWritten = _metricsRepository.Metrics[monitor2].Take(5);
            Assert.Equal(testMetrics2, metricsWritten);
        }
        [Fact]
        public async Task OnTimer_ProbesTargetScalers_FallsBackToMonitor()
        {
            // Arrange: create a fresh service with TBS enabled
            var monitors = new List<IScaleMonitor>
            {
                new TestScaleMonitor<ScaleMetrics>("function1-test-test", "function1")
            };
            var scalers = new List<ITargetScaler>
            {
                new FaultyTargetScaler
                {
                    TargetScalerDescriptor = new TargetScalerDescriptor("function1")
                }
            };

            var monitorManagerMock = new Mock<IScaleMonitorManager>(MockBehavior.Strict);
            monitorManagerMock.Setup(p => p.GetMonitors()).Returns(() => monitors);
            var targetScalerManagerMock = new Mock<ITargetScalerManager>(MockBehavior.Strict);
            targetScalerManagerMock.Setup(p => p.GetTargetScalers()).Returns(() => scalers);

            var loggerProvider = new TestLoggerProvider();
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(loggerProvider);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string> { { "Microsoft.Azure.WebJobs.Host.UnitTests", "1" } }).Build();

            var options = Options.Create(new ScaleOptions
            {
                ScaleMetricsSampleInterval = TimeSpan.FromSeconds(1),
                IsRuntimeScalingEnabled = true,
                IsTargetScalingEnabled = true
            });

            var primaryHostState = new PrimaryHostStateProvider { IsPrimary = true };

            // Clear static state from prior tests
            ScaleManager._targetScalersInError.Clear();

            var service = new ScaleMonitorService(
                new Mock<ScaleManager>().Object,
                new TestMetricsRepository(),
                options,
                primaryHostState,
                monitorManagerMock.Object,
                targetScalerManagerMock.Object,
                configuration,
                loggerFactory);

            // Before starting: target scaler is not in error, so GetScalersToSample returns it as target scaler
            var (monitors1, scalers1) = ScaleManager.GetScalersToSample(
                monitorManagerMock.Object, targetScalerManagerMock.Object, options, configuration);
            Assert.Equal(0, monitors1.Count);
            Assert.Equal(1, scalers1.Count);

            // Act: start the service — the timer tick will probe the faulty target scaler
            await service.StartAsync(CancellationToken.None);

            await TestHelpers.Await(() =>
            {
                var logs = loggerProvider.GetAllLogMessages();
                return logs.Any(l => l.FormattedMessage.Contains("Unable to use target based scaling"));
            });

            await service.StopAsync(CancellationToken.None);

            // Assert: after probing, the faulty scaler is in _targetScalersInError
            // so GetScalersToSample now returns the incremental monitor instead
            var (monitors2, scalers2) = ScaleManager.GetScalersToSample(
                monitorManagerMock.Object, targetScalerManagerMock.Object, options, configuration);
            Assert.Equal(1, monitors2.Count);
            Assert.Equal(0, scalers2.Count);

            // Verify the log message includes the ScaleMonitorService caller
            var errorLogs = loggerProvider.GetAllLogMessages()
                .Where(l => l.FormattedMessage.Contains("Unable to use target based scaling"))
                .ToArray();
            Assert.Single(errorLogs);
            Assert.Contains("Detected by: ScaleMonitorService", errorLogs[0].FormattedMessage);
        }
    }

    public class TestMetricsRepository : IScaleMetricsRepository
    {
        private int _count;

        public TestMetricsRepository()
        {
            _count = 0;
            Metrics = new Dictionary<IScaleMonitor, IList<ScaleMetrics>>();
        }

        public int Count => _count;

        public IDictionary<IScaleMonitor, IList<ScaleMetrics>> Metrics { get; set; }

        public Task<IDictionary<IScaleMonitor, IList<ScaleMetrics>>> ReadMetricsAsync(IEnumerable<IScaleMonitor> monitors)
        {
            return Task.FromResult<IDictionary<IScaleMonitor, IList<ScaleMetrics>>>(Metrics);
        }

        public Task WriteMetricsAsync(IDictionary<IScaleMonitor, ScaleMetrics> monitorMetrics)
        {
            foreach (var pair in monitorMetrics)
            {
                if (!Metrics.ContainsKey(pair.Key))
                {
                    Metrics[pair.Key] = new List<ScaleMetrics>();
                }

                Metrics[pair.Key].Add(pair.Value);

                Interlocked.Increment(ref _count);
            }

            return Task.CompletedTask;
        }
    }
}
