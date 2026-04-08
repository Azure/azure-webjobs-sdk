// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Scale
{
    [Trait(TestTraits.CategoryTraitName, TestTraits.ScaleMonitoring)]
    public class ScaleManagerTests
    {
        private readonly Mock<IScaleMonitorManager> _monitorManagerMock;
        private readonly Mock<IScaleMetricsRepository> _metricsRepositoryMock;
        private readonly Mock<ITargetScalerManager> _targetScalerManagerMock;
        private readonly Mock<IConcurrencyStatusRepository> _concurrencyStatusRepositoryMock;
        private readonly ILoggerFactory _loggerFactory;
        private readonly TestLoggerProvider _loggerProvider;
        private readonly List<IScaleMonitor> _monitors;
        private readonly List<ITargetScaler> _targetScalers;
        private readonly ILogger _testLogger;
        private readonly IOptions<ScaleOptions> _scaleOptions;
        private readonly IOptions<ConcurrencyOptions> _concurrencyOptions;
        private readonly IConfiguration _configuration;
        private readonly ITargetScalerErrorRepository _targetScalerErrorRepository;

        public ScaleManagerTests()
        {
            _monitors = new List<IScaleMonitor>();
            _targetScalers = new List<ITargetScaler>();
            _loggerProvider = new TestLoggerProvider();
            _loggerFactory = new LoggerFactory();
            _loggerFactory.AddProvider(_loggerProvider);
            _testLogger = _loggerFactory.CreateLogger<ScaleManagerTests>();

            _monitorManagerMock = new Mock<IScaleMonitorManager>(MockBehavior.Strict);
            _monitorManagerMock.Setup(p => p.GetMonitors()).Returns(() => _monitors);
            _targetScalerManagerMock = new Mock<ITargetScalerManager>(MockBehavior.Strict);
            _targetScalerManagerMock.Setup(p => p.GetTargetScalers()).Returns(() => _targetScalers);
            _metricsRepositoryMock = new Mock<IScaleMetricsRepository>(MockBehavior.Strict);
            _metricsRepositoryMock.Setup(x => x.ReadMetricsAsync(It.IsAny<IEnumerable<IScaleMonitor>>())).ReturnsAsync(new Dictionary<IScaleMonitor, IList<ScaleMetrics>>());
            _concurrencyStatusRepositoryMock = new Mock<IConcurrencyStatusRepository>(MockBehavior.Strict);
            _concurrencyStatusRepositoryMock.Setup(p => p.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
                new HostConcurrencySnapshot()
                {
                    FunctionSnapshots = new Dictionary<string, FunctionConcurrencySnapshot>()
                    {
                        { "func1", new FunctionConcurrencySnapshot() { Concurrency = 1 } }
                    }
                });
            _scaleOptions = Options.Create(new ScaleOptions()
            {
                IsTargetScalingEnabled = true,
                ScaleMetricsSampleInterval = TimeSpan.FromSeconds(10)
            });

            _concurrencyOptions = Options.Create(new ConcurrencyOptions()
            {
                DynamicConcurrencyEnabled = true,
                SnapshotPersistenceEnabled = true
            });

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string> { { "Microsoft.Azure.WebJobs.Host.UnitTests", "1" } }).Build();

            _targetScalerErrorRepository = new InMemoryTargetScalerErrorRepository();
        }

        [Theory]
        [InlineData(0, ScaleVote.None)]
        [InlineData(1, ScaleVote.ScaleIn)]
        public async Task GetScaleStatus_NoMonitors_ReturnsExpectedStatus(int workerCount, ScaleVote expected)
        {
            var context = new ScaleStatusContext
            {
                WorkerCount = workerCount
            };
            ScaleManager scaleManager = new ScaleManager(_monitorManagerMock.Object, _targetScalerManagerMock.Object, _metricsRepositoryMock.Object, _concurrencyStatusRepositoryMock.Object, _targetScalerErrorRepository, _scaleOptions, _loggerFactory, _configuration);
            var status = await scaleManager.GetScaleStatusAsync(context);

            Assert.Equal(expected, status.Vote);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetScaleStatus_ReturnsExpectedResult(bool tbsEnabled)
        {
            var context = new ScaleStatusContext
            {
                WorkerCount = 3
            };

            var monitor1 = new TestScaleMonitor<TestScaleMetrics1>("func1-test-test", "func1");
            monitor1.Status = new ScaleStatus
            {
                Vote = ScaleVote.ScaleIn
            };
            var monitor2 = new TestScaleMonitor1();
            monitor2.Status = new ScaleStatus
            {
                Vote = ScaleVote.ScaleOut
            };
            var monitor3 = new TestScaleMonitor2();
            monitor3.Status = new ScaleStatus
            {
                Vote = ScaleVote.ScaleIn
            };

            List<IScaleMonitor> monitors = new List<IScaleMonitor>
            {
                monitor1
            };
            if (!tbsEnabled)
            {
                monitors.Add(monitor2);
                monitors.Add(monitor3);
            }
            _monitorManagerMock.Setup(p => p.GetMonitors()).Returns(monitors);

            var monitorMetrics = new Dictionary<IScaleMonitor, IList<ScaleMetrics>>
            {
                { monitor1, new List<ScaleMetrics>() },
                { monitor2, new List<ScaleMetrics>() },
                { monitor3, new List<ScaleMetrics>() }
            };
            _metricsRepositoryMock.Setup(p => p.ReadMetricsAsync(It.IsAny<IEnumerable<IScaleMonitor>>())).ReturnsAsync(monitorMetrics);

            var targetScaler1 = new TestTargetScaler()
            {
                Result = new TargetScalerResult()
                {
                    TargetWorkerCount = 2
                },
                TargetScalerDescriptor = new TargetScalerDescriptor("func1")
            };

            _targetScalerManagerMock.Setup(p => p.GetTargetScalers()).Returns(new List<ITargetScaler> { targetScaler1 });

            IOptions<ScaleOptions> options = Options.Create(new ScaleOptions
            {
                IsTargetScalingEnabled = tbsEnabled,
            });

            // Pass ConcurrencyOptions with DC enabled when TBS is enabled to exercise the concurrency snapshot path
            ScaleManager scaleManager = new ScaleManager(_monitorManagerMock.Object, _targetScalerManagerMock.Object, _metricsRepositoryMock.Object, _concurrencyStatusRepositoryMock.Object, _targetScalerErrorRepository, options, _loggerFactory, _configuration, tbsEnabled ? _concurrencyOptions : null);

            var status = await scaleManager.GetScaleStatusAsync(context);

            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            if (!tbsEnabled)
            {
                Assert.Equal("Computing scale status (WorkerCount=3)", logs[0].FormattedMessage);
                Assert.Equal("3 scale monitors to sample", logs[1].FormattedMessage);
                Assert.Equal("Function 'func1' vote: 'ScaleIn'.", logs[2].FormattedMessage);
                Assert.Equal("Function 'testscalemonitor1' vote: 'ScaleOut'.", logs[3].FormattedMessage);
                Assert.Equal("Function 'testscalemonitor2' vote: 'ScaleIn'.", logs[4].FormattedMessage);
                Assert.Equal(ScaleVote.ScaleOut, status.Vote);
                Assert.Equal(null, status.TargetWorkerCount);
            }
            else
            {
                Assert.Equal("1 target scalers to sample", logs[0].FormattedMessage);
                Assert.Equal("Snapshot dynamic concurrency for target scaler 'func1' is '1'", logs[1].FormattedMessage);
                Assert.Equal("Function 'func1' vote: TargetWorkerCount='2'.", logs[2].FormattedMessage);
                Assert.Equal(ScaleVote.ScaleIn, status.Vote);
                Assert.Equal(2, status.TargetWorkerCount);
            }
        }

        [Fact]
        public async Task GetScaleStatus_MonitorFails_ReturnsExpectedResult()
        {
            var context = new ScaleStatusContext
            {
                WorkerCount = 3
            };

            var mockMonitor1 = new Mock<IScaleMonitor>(MockBehavior.Strict);
            mockMonitor1.Setup(p => p.GetScaleStatus(It.Is<ScaleStatusContext>(q => q.WorkerCount == context.WorkerCount))).Returns(new ScaleStatus { Vote = ScaleVote.ScaleIn });
            mockMonitor1.SetupGet(p => p.Descriptor).Returns(new ScaleMonitorDescriptor("testscalemonitor1"));
            var mockMonitor2 = new Mock<IScaleMonitor>(MockBehavior.Strict);
            mockMonitor2.SetupGet(p => p.Descriptor).Returns(new ScaleMonitorDescriptor("testscalemonitor2"));
            var exception = new Exception("Kaboom!");
            mockMonitor2.Setup(p => p.GetScaleStatus(It.Is<ScaleStatusContext>(q => q.WorkerCount == context.WorkerCount))).Throws(exception);
            var mockMonitor3 = new Mock<IScaleMonitor>(MockBehavior.Strict);
            mockMonitor3.Setup(p => p.GetScaleStatus(It.Is<ScaleStatusContext>(q => q.WorkerCount == context.WorkerCount))).Returns(new ScaleStatus { Vote = ScaleVote.ScaleIn });
            mockMonitor3.SetupGet(p => p.Descriptor).Returns(new ScaleMonitorDescriptor("testscalemonitor3"));
            List<IScaleMonitor> monitors = new List<IScaleMonitor>
            {
                mockMonitor1.Object,
                mockMonitor2.Object,
                mockMonitor3.Object
            };

            _monitorManagerMock.Setup(p => p.GetMonitors()).Returns(monitors);

            var monitorMetrics = new Dictionary<IScaleMonitor, IList<ScaleMetrics>>
            {
                { mockMonitor1.Object, new List<ScaleMetrics>() },
                { mockMonitor2.Object, new List<ScaleMetrics>() },
                { mockMonitor3.Object, new List<ScaleMetrics>() }
            };
            _metricsRepositoryMock.Setup(p => p.ReadMetricsAsync(It.IsAny<IEnumerable<IScaleMonitor>>())).ReturnsAsync(monitorMetrics);

            ScaleManager scaleManager = new ScaleManager(_monitorManagerMock.Object, _targetScalerManagerMock.Object, _metricsRepositoryMock.Object, _concurrencyStatusRepositoryMock.Object, _targetScalerErrorRepository, _scaleOptions, _loggerFactory, _configuration);
            var status = await scaleManager.GetScaleStatusAsync(context);

            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            Assert.Equal("Computing scale status (WorkerCount=3)", logs[0].FormattedMessage);
            Assert.Equal("3 scale monitors to sample", logs[1].FormattedMessage);
            Assert.Equal("Function 'testscalemonitor1' vote: 'ScaleIn'.", logs[2].FormattedMessage);
            Assert.Equal("Function 'testscalemonitor2' error: Failed to get scale monitor vote.", logs[3].FormattedMessage);
            Assert.Same(exception, logs[3].Exception);
            Assert.Equal("Function 'testscalemonitor3' vote: 'ScaleIn'.", logs[4].FormattedMessage);

            Assert.Equal(null, status.TargetWorkerCount);
            Assert.Equal(ScaleVote.ScaleIn, status.Vote);
        }

        [Fact]
        public async Task GetScaleStatus_TargetScalerFails_ReturnsExpectedResult()
        {
            var context = new ScaleStatusContext
            {
                WorkerCount = 3
            };

            var targetScaler1 = new TestTargetScaler { Result = new TargetScalerResult { TargetWorkerCount = 3 }, TargetScalerDescriptor = new TargetScalerDescriptor("func1") };
            var targetScaler2 = new TestTargetScaler2 { Result = new TargetScalerResult { TargetWorkerCount = 1 }, TargetScalerDescriptor = new TargetScalerDescriptor("func2") };
            var targetScaler3 = new TestTargetScaler { Result = new TargetScalerResult { TargetWorkerCount = -3 }, TargetScalerDescriptor = new TargetScalerDescriptor("func3") };
            List<ITargetScaler> targetScalers = new List<ITargetScaler>
            {
                targetScaler1,
                targetScaler2,
                targetScaler3
            };
            _targetScalerManagerMock.Setup(p => p.GetTargetScalers()).Returns(targetScalers);

            IOptions<ScaleOptions> options = Options.Create(new ScaleOptions
            {
                IsTargetScalingEnabled = true,
            });

            ScaleManager scaleManager = new ScaleManager(_monitorManagerMock.Object, _targetScalerManagerMock.Object, _metricsRepositoryMock.Object, _concurrencyStatusRepositoryMock.Object, _targetScalerErrorRepository, options, _loggerFactory, _configuration, _concurrencyOptions);

            var status = await scaleManager.GetScaleStatusAsync(context);

            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            Assert.Equal("3 target scalers to sample", logs[0].FormattedMessage);
            Assert.Equal($"Snapshot dynamic concurrency for target scaler 'func1' is '1'", logs[1].FormattedMessage);
            Assert.Equal("Function 'func1' vote: TargetWorkerCount='3'.", logs[2].FormattedMessage);
            Assert.Equal("Function 'func2' error: Failed to get target scaler vote.", logs[3].FormattedMessage);
            Assert.Same("test", logs[3].Exception.Message);
            Assert.Equal("Function 'func3' vote: TargetWorkerCount='-3'.", logs[4].FormattedMessage);

            Assert.Equal(3, status.TargetWorkerCount);
            Assert.Equal(ScaleVote.None, status.Vote);
        }

        [Theory]
        [InlineData(0, 0, 0, ScaleVote.None)]
        [InlineData(1, 0, 0, ScaleVote.ScaleIn)]
        [InlineData(1, 1, 3, ScaleVote.ScaleOut)]
        [InlineData(0, 0, 1, ScaleVote.None)]
        [InlineData(1, 0, 1, ScaleVote.ScaleIn)]
        [InlineData(5, 0, 3, ScaleVote.ScaleIn)]
        public void GetAggregateScaleVote_ReturnsExpectedResult(int workerCount, int numScaleOutVotes, int numScaleInVotes, ScaleVote expected)
        {
            var context = new ScaleStatusContext
            {
                WorkerCount = workerCount
            };
            List<ScaleVote> votes = new List<ScaleVote>();
            for (int i = 0; i < numScaleOutVotes; i++)
            {
                votes.Add(ScaleVote.ScaleOut);
            }
            for (int i = 0; i < numScaleInVotes; i++)
            {
                votes.Add(ScaleVote.ScaleIn);
            }
            var vote = ScaleManager.GetAggregateScaleVote(votes, context, _testLogger);
            Assert.Equal(expected, vote);
        }

        [Theory]
        [InlineData(false, true, 1, 0)]
        [InlineData(true, false, 1, 0)]
        [InlineData(true, true, 0, 1)]
        public void GetScalersToSample_Returns_Expected(bool targetBaseScalingEnabled, bool triggerEnabled, int expectedScaleMonitorCount, int expectedTargetScalerCount)
        {
            List<IScaleMonitor> scaleMonitors = new List<IScaleMonitor>
            {
                new TestScaleMonitor<ScaleMetrics>("func1-test-test", "func1"),
            };
            Mock<IScaleMonitorManager> scaleMonitorManagerMock = new Mock<IScaleMonitorManager>(MockBehavior.Strict);
            scaleMonitorManagerMock.Setup(x => x.GetMonitors()).Returns(scaleMonitors);

            List<ITargetScaler> targetScalers = new List<ITargetScaler>
            {
                new TestTargetScaler()
                {
                    TargetScalerDescriptor = new TargetScalerDescriptor("func1")
                }
            };
            Mock<ITargetScalerManager> targetScalerManagerMock = new Mock<ITargetScalerManager>(MockBehavior.Strict);
            targetScalerManagerMock.Setup(x => x.GetTargetScalers()).Returns(targetScalers);

            IOptions<ScaleOptions> options = Options.Create(new ScaleOptions
            {
                IsTargetScalingEnabled = targetBaseScalingEnabled,
            });

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string> { { "Microsoft.Azure.WebJobs.Host.UnitTests", triggerEnabled ? "1" : "0" } }).Build();


            var (scaleMonitorsToProcess, targetScalesToProcess) = ScaleManager.GetScalersToSample(
                scaleMonitorManagerMock.Object,
                targetScalerManagerMock.Object,
                options,
                configuration
                );

            Assert.Equal(scaleMonitorsToProcess.Count(), expectedScaleMonitorCount);
            Assert.Equal(targetScalesToProcess.Count(), expectedTargetScalerCount);
        }

        [Fact]
        public async Task GetScalersToSample_FallsBackToMonitor_OnTargetScalerError()
        {
            List<IScaleMonitor> scaleMonitors = new List<IScaleMonitor>
            {
                new TestScaleMonitor<ScaleMetrics>("function1-test-test", "function1")
            };
            Mock<IScaleMonitorManager> scaleMonitorManagerMock = new Mock<IScaleMonitorManager>(MockBehavior.Strict);
            scaleMonitorManagerMock.Setup(x => x.GetMonitors()).Returns(scaleMonitors);

            List<ITargetScaler> targetScalers = new List<ITargetScaler>
            {
                new FaultyTargetScaler()
                {
                    TargetScalerDescriptor = new TargetScalerDescriptor("function1")
                },
                new TestTargetScaler2()
                {
                    TargetScalerDescriptor = new TargetScalerDescriptor("function2")
                }
            };
            Mock<ITargetScalerManager> targetScalerManagerMock = new Mock<ITargetScalerManager>(MockBehavior.Strict);
            targetScalerManagerMock.Setup(x => x.GetTargetScalers()).Returns(targetScalers);

            var context = new ScaleStatusContext()
            {
                WorkerCount = 1
            };

            IOptions<ScaleOptions> options = Options.Create(new ScaleOptions
            {
                IsTargetScalingEnabled = true,
            });

            ScaleManager scaleManager = new ScaleManager(scaleMonitorManagerMock.Object, targetScalerManagerMock.Object, _metricsRepositoryMock.Object, _concurrencyStatusRepositoryMock.Object, _targetScalerErrorRepository, options, _loggerFactory, _configuration);

            var (monitors1, scalers1) = ScaleManager.GetScalersToSample( //Col1
                scaleMonitorManagerMock.Object,
                targetScalerManagerMock.Object,
                _scaleOptions,
                _configuration
                );
            Assert.Equal(monitors1.Count(), 0);
            Assert.Equal(scalers1.Count(), 2);
            AggregateScaleStatus result1 = await scaleManager.GetScaleStatusAsync(context); // Col2

            Assert.Equal(result1.TargetWorkerCount, 1);
            Assert.Equal(result1.Vote, ScaleVote.None);
            var logs = _loggerProvider.GetAllLogMessages().Select(x => x.FormattedMessage).ToArray();
            Assert.Single(logs, x => x == "Function 'function1' error: Unable to use target based scaling, switching to metrics monitor.");
            _loggerProvider.ClearAllLogMessages();

            // After the error is recorded, subsequent calls should see the fallback
            var scalersInError = await _targetScalerErrorRepository.GetAsync(CancellationToken.None);
            var (monitors2, scalers2) = ScaleManager.GetScalersToSample(
                scaleMonitorManagerMock.Object,
                targetScalerManagerMock.Object,
                _scaleOptions,
                _configuration,
                scalersInError
                );
            Assert.Equal(monitors2.Count(), 1);
            Assert.Equal(scalers2.Count(), 1);
            AggregateScaleStatus result2 = await scaleManager.GetScaleStatusAsync(context);
            Assert.Equal(result2.TargetWorkerCount, null);
            Assert.Equal(result2.Vote, ScaleVote.ScaleIn);
            logs = _loggerProvider.GetAllLogMessages().Select(x => x.FormattedMessage).ToArray();
            Assert.DoesNotContain(logs, x => x == "Function 'function1' error: Unable to use target based scaling, switching to metrics monitor.");
        }

        [Fact]
        public async Task GetTargetScalersResult_DynamicConcurrencyDisabled_DoesNotReadConcurrencyStatus()
        {
            var context = new ScaleStatusContext { WorkerCount = 1 };

            var targetScaler1 = new TestTargetScaler
            {
                Result = new TargetScalerResult { TargetWorkerCount = 2 },
                TargetScalerDescriptor = new TargetScalerDescriptor("func1")
            };
            _targetScalerManagerMock.Setup(p => p.GetTargetScalers()).Returns(new List<ITargetScaler> { targetScaler1 });

            IOptions<ScaleOptions> options = Options.Create(new ScaleOptions { IsTargetScalingEnabled = true });

            // No ConcurrencyOptions passed - DC is disabled by default
            var concurrencyStatusRepositoryMock = new Mock<IConcurrencyStatusRepository>(MockBehavior.Strict);
            // No setup for ReadAsync - if it is called the strict mock will throw

            ScaleManager scaleManager = new ScaleManager(_monitorManagerMock.Object, _targetScalerManagerMock.Object, _metricsRepositoryMock.Object, concurrencyStatusRepositoryMock.Object, _targetScalerErrorRepository, options, _loggerFactory, _configuration);

            var status = await scaleManager.GetScaleStatusAsync(context);

            // Verify ReadAsync was never called
            concurrencyStatusRepositoryMock.Verify(p => p.ReadAsync(It.IsAny<CancellationToken>()), Times.Never);

            // Verify the snapshot log is not present
            var logs = _loggerProvider.GetAllLogMessages().Select(x => x.FormattedMessage).ToArray();
            Assert.DoesNotContain(logs, x => x.StartsWith("Snapshot dynamic concurrency"));

            Assert.Equal(2, status.TargetWorkerCount);
        }

        [Theory]
        [InlineData(true, true, true)]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(false, false, false)]
        public async Task GetTargetScalersResult_ReadsConcurrencyStatus_OnlyWhenDCAndPersistenceEnabled(bool dynamicConcurrencyEnabled, bool snapshotPersistenceEnabled, bool expectReadCalled)
        {
            var context = new ScaleStatusContext { WorkerCount = 1 };

            var targetScaler1 = new TestTargetScaler
            {
                Result = new TargetScalerResult { TargetWorkerCount = 2 },
                TargetScalerDescriptor = new TargetScalerDescriptor("func1")
            };
            _targetScalerManagerMock.Setup(p => p.GetTargetScalers()).Returns(new List<ITargetScaler> { targetScaler1 });

            IOptions<ScaleOptions> options = Options.Create(new ScaleOptions { IsTargetScalingEnabled = true });
            IOptions<ConcurrencyOptions> concurrencyOptions = Options.Create(new ConcurrencyOptions
            {
                DynamicConcurrencyEnabled = dynamicConcurrencyEnabled,
                SnapshotPersistenceEnabled = snapshotPersistenceEnabled
            });

            var concurrencyStatusRepositoryMock = new Mock<IConcurrencyStatusRepository>(MockBehavior.Strict);
            if (expectReadCalled)
            {
                concurrencyStatusRepositoryMock.Setup(p => p.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
                    new HostConcurrencySnapshot
                    {
                        FunctionSnapshots = new Dictionary<string, FunctionConcurrencySnapshot>
                        {
                            { "func1", new FunctionConcurrencySnapshot { Concurrency = 5 } }
                        }
                    });
            }

            ScaleManager scaleManager = new ScaleManager(_monitorManagerMock.Object, _targetScalerManagerMock.Object, _metricsRepositoryMock.Object, concurrencyStatusRepositoryMock.Object, _targetScalerErrorRepository, options, _loggerFactory, _configuration, concurrencyOptions);

            await scaleManager.GetScaleStatusAsync(context);

            var expectedCalls = expectReadCalled ? Times.Once() : Times.Never();
            concurrencyStatusRepositoryMock.Verify(p => p.ReadAsync(It.IsAny<CancellationToken>()), expectedCalls);
        }
    }
}
