// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Scale
{
    [Trait(TestTraits.CategoryTraitName, TestTraits.DynamicConcurrency)]
    public class ConcurrencyManagerTests
    {
        private const string TestFunctionId = "testfunction";

        private readonly ConcurrencyManager _concurrencyManager;
        private readonly LoggerFactory _loggerFactory;
        private readonly TestLoggerProvider _loggerProvider;
        private readonly Mock<IConcurrencyThrottleManager> _mockThrottleManager;
        private readonly ConcurrencyStatus _testFunctionConcurrencyStatus;

        private ConcurrencyThrottleAggregateStatus _throttleStatus;

        public ConcurrencyManagerTests()
        {
            var options = new ConcurrencyOptions
            {
                DynamicConcurrencyEnabled = true
            };
            var optionsWrapper = new OptionsWrapper<ConcurrencyOptions>(options);
            _loggerFactory = new LoggerFactory();
            _loggerProvider = new TestLoggerProvider();
            _loggerFactory.AddProvider(_loggerProvider);

            _mockThrottleManager = new Mock<IConcurrencyThrottleManager>(MockBehavior.Strict);
            _mockThrottleManager.Setup(p => p.GetStatus()).Returns(() => _throttleStatus);

            _concurrencyManager = new ConcurrencyManager(optionsWrapper, _loggerFactory, _mockThrottleManager.Object);

            _testFunctionConcurrencyStatus = new ConcurrencyStatus(TestFunctionId, _concurrencyManager);
            _concurrencyManager.ConcurrencyStatuses[TestFunctionId] = _testFunctionConcurrencyStatus;
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Enabled_ReturnsExpectedResult(bool enabled)
        {
            var options = new ConcurrencyOptions
            {
                DynamicConcurrencyEnabled = enabled
            };
            var optionsWrapper = new OptionsWrapper<ConcurrencyOptions>(options);
            var concurrencyManager = new ConcurrencyManager(optionsWrapper, _loggerFactory, _mockThrottleManager.Object);

            Assert.Equal(concurrencyManager.Enabled, enabled);
        }

        [Fact]
        public void ThrottleStatus_ReturnsExpectedResult()
        {
            _throttleStatus = new ConcurrencyThrottleAggregateStatus { State = ThrottleState.Disabled };

            // set the last adjustment outside of the throttle window so GetStatus
            // won't short circuit
            TestHelpers.SetupStopwatch(_testFunctionConcurrencyStatus.LastConcurrencyAdjustmentStopwatch, TimeSpan.FromSeconds(ConcurrencyStatus.DefaultMinAdjustmentFrequencySeconds + 1));

            var concurrencyStatus = _concurrencyManager.GetStatus(TestFunctionId);

            Assert.Same(_throttleStatus, _concurrencyManager.ThrottleStatus);
            Assert.Same(_throttleStatus, concurrencyStatus.ThrottleStatus);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void GetStatus_InvalidFunction_Throws(string functionId)
        {
            var ex = Assert.Throws<ArgumentNullException>(() => _concurrencyManager.GetStatus(functionId));
            Assert.Equal(nameof(functionId), ex.ParamName);
        }

        [Fact]
        public void GetStatus_ThrottleDisabled_IncreasesConcurrency()
        {
            _throttleStatus = new ConcurrencyThrottleAggregateStatus { State = ThrottleState.Disabled, ConsecutiveCount = ConcurrencyManager.MinConsecutiveIncreaseLimit };

            TestHelpers.SetupStopwatch(_testFunctionConcurrencyStatus.LastConcurrencyAdjustmentStopwatch, TimeSpan.FromSeconds(ConcurrencyStatus.DefaultMinAdjustmentFrequencySeconds + 1));

            int prevConcurrency = _testFunctionConcurrencyStatus.CurrentConcurrency;
            SimulateFunctionInvocations(TestFunctionId, 3);

            var status = _concurrencyManager.GetStatus(TestFunctionId);

            Assert.Equal(prevConcurrency + 1, status.CurrentConcurrency);

            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            Assert.Equal(2, logs.Length);

            var log = logs[0];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal(LogCategories.Concurrency, log.Category);
            Assert.Equal("testfunction Increasing concurrency", log.FormattedMessage);

            log = logs[1];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal(LogCategories.Concurrency, log.Category);
            Assert.Equal("testfunction Concurrency: 2, OutstandingInvocations: 0", log.FormattedMessage);
        }

        [Fact]
        public void GetStatus_ThrottleEnabled_DecreasesConcurrency()
        {
            _throttleStatus = new ConcurrencyThrottleAggregateStatus 
            { 
                State = ThrottleState.Enabled, 
                ConsecutiveCount = ConcurrencyManager.MinConsecutiveDecreaseLimit,
                EnabledThrottles = new List<string> { DefaultHostProcessMonitor.CpuLimitName, ThreadPoolStarvationThrottleProvider.ThreadPoolStarvationThrottleName }
            };

            TestHelpers.SetupStopwatch(_testFunctionConcurrencyStatus.LastConcurrencyAdjustmentStopwatch, TimeSpan.FromSeconds(ConcurrencyStatus.DefaultMinAdjustmentFrequencySeconds));

            _testFunctionConcurrencyStatus.CurrentConcurrency = 5;
            SimulateFunctionInvocations(TestFunctionId, 3);

            var status = _concurrencyManager.GetStatus(TestFunctionId);

            Assert.Equal(4, status.CurrentConcurrency);

            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            Assert.Equal(2, logs.Length);

            var log = logs[0];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal(LogCategories.Concurrency, log.Category);
            Assert.Equal("testfunction Decreasing concurrency (Enabled throttles: CPU,ThreadPoolStarvation)", log.FormattedMessage);

            log = logs[1];
            Assert.Equal(LogLevel.Debug, log.Level);
            Assert.Equal(LogCategories.Concurrency, log.Category);
            Assert.Equal("testfunction Concurrency: 4, OutstandingInvocations: 0", log.FormattedMessage);
        }

        [Fact]
        public void GetStatus_ThrottleUnknown_ReturnsCurrentStatus()
        {
            _throttleStatus = new ConcurrencyThrottleAggregateStatus { State = ThrottleState.Unknown };

            TestHelpers.SetupStopwatch(_testFunctionConcurrencyStatus.LastConcurrencyAdjustmentStopwatch, TimeSpan.FromSeconds(ConcurrencyStatus.DefaultMinAdjustmentFrequencySeconds));

            _testFunctionConcurrencyStatus.CurrentConcurrency = 1;

            var status = _concurrencyManager.GetStatus(TestFunctionId);

            Assert.Equal(1, status.CurrentConcurrency);
        }

        [Fact]
        public void GetStatus_FunctionRecentlyAdjusted_ReturnsCurrentStatus()
        {
            _testFunctionConcurrencyStatus.LastConcurrencyAdjustmentStopwatch.Restart();

            var status = _concurrencyManager.GetStatus(TestFunctionId);

            _mockThrottleManager.Verify(p => p.GetStatus(), Times.Never);
        }

        [Fact]
        public void GetStatus_OnlyLogsOnStatusChange()
        {
            TestHelpers.SetupStopwatch(_testFunctionConcurrencyStatus.LastConcurrencyAdjustmentStopwatch, TimeSpan.FromSeconds(ConcurrencyStatus.DefaultMinAdjustmentFrequencySeconds));
            _throttleStatus = new ConcurrencyThrottleAggregateStatus { State = ThrottleState.Disabled };

            // make a bunch of back to back GetStatus calls
            // only expect a log entry for the first one
            ConcurrencyStatus status = null;
            for (int i = 0; i < 10; i++)
            {
                status = _concurrencyManager.GetStatus(TestFunctionId);
            }
            Assert.Equal(1, status.CurrentConcurrency);
            var logs = _loggerProvider.GetAllLogMessages().Where(p => p.Category == LogCategories.Concurrency).ToArray();
            Assert.Single(logs);
            Assert.Equal("testfunction Concurrency: 1, OutstandingInvocations: 0", logs[0].FormattedMessage);

            // now increase concurrency - expect a single log
            _loggerProvider.ClearAllLogMessages();
            status.IncreaseConcurrency();
            TestHelpers.SetupStopwatch(_testFunctionConcurrencyStatus.LastConcurrencyAdjustmentStopwatch, TimeSpan.FromSeconds(ConcurrencyStatus.DefaultMinAdjustmentFrequencySeconds));
            status = _concurrencyManager.GetStatus(TestFunctionId);
            Assert.Equal(2, status.CurrentConcurrency);
            logs = _loggerProvider.GetAllLogMessages().Where(p => p.Category == LogCategories.Concurrency).ToArray();
            Assert.Single(logs);
            Assert.Equal("testfunction Concurrency: 2, OutstandingInvocations: 0", logs[0].FormattedMessage);

            // make a bunch more back to back GetStatus calls without
            // changing anything - don't expect any logs
            _loggerProvider.ClearAllLogMessages();
            for (int i = 0; i < 10; i++)
            {
                status = _concurrencyManager.GetStatus(TestFunctionId);
            }
            Assert.Equal(2, status.CurrentConcurrency);
            logs = _loggerProvider.GetAllLogMessages().Where(p => p.Category == LogCategories.Concurrency).ToArray();
            Assert.Empty(logs);
        }

        [Theory]
        [InlineData(50, true)]
        [InlineData(1, false)]
        public void CanDecreaseConcurrency_ReturnsExpectedValue(int concurrency, bool expected)
        {
            _testFunctionConcurrencyStatus.CurrentConcurrency = concurrency;
            Assert.Equal(expected, _testFunctionConcurrencyStatus.CanDecreaseConcurrency());
        }

        [Fact]
        public void FunctionInvocationTracking_MaintainsExpectedCounts()
        {
            // FunctionStarted/FunctionCompleted only operates on functions
            // that are DC enabled, so we must prime the pump here
            _concurrencyManager.GetStatus("testfunction1");
            _concurrencyManager.GetStatus("testfunction2");
            _concurrencyManager.GetStatus("testfunction3");

            // simulate some function invocations and verify bookkeeping
            _concurrencyManager.FunctionStarted("testfunction1");

            _concurrencyManager.FunctionStarted("testfunction1");
            _concurrencyManager.FunctionCompleted("testfunction1", TimeSpan.FromMilliseconds(110));

            _concurrencyManager.FunctionStarted("testfunction1");
            _concurrencyManager.FunctionStarted("testfunction1");

            var status = _concurrencyManager.ConcurrencyStatuses["testfunction1"];
            Assert.Equal(3, status.OutstandingInvocations);
            Assert.Equal(3, status.MaxConcurrentExecutionsSinceLastAdjustment);

            _concurrencyManager.FunctionStarted("testfunction2");
            _concurrencyManager.FunctionStarted("testfunction2");

            status = _concurrencyManager.ConcurrencyStatuses["testfunction2"];
            Assert.Equal(2, status.OutstandingInvocations);
            Assert.Equal(2, status.MaxConcurrentExecutionsSinceLastAdjustment);

            _concurrencyManager.FunctionStarted("testfunction3");

            status = _concurrencyManager.ConcurrencyStatuses["testfunction3"];
            Assert.Equal(1, status.OutstandingInvocations);
            Assert.Equal(1, status.MaxConcurrentExecutionsSinceLastAdjustment);

            // complete the invocations and verify bookkeeping
            _concurrencyManager.FunctionCompleted("testfunction1", TimeSpan.FromMilliseconds(100));
            _concurrencyManager.FunctionCompleted("testfunction1", TimeSpan.FromMilliseconds(150));
            _concurrencyManager.FunctionCompleted("testfunction1", TimeSpan.FromMilliseconds(90));

            status = _concurrencyManager.ConcurrencyStatuses["testfunction1"];
            Assert.Equal(0, status.OutstandingInvocations);
            Assert.Equal(3, status.MaxConcurrentExecutionsSinceLastAdjustment);
            Assert.Equal(4, status.InvocationsSinceLastAdjustment);
            Assert.Equal(450, status.TotalInvocationTimeSinceLastAdjustmentMs);

            _concurrencyManager.FunctionCompleted("testfunction2", TimeSpan.FromMilliseconds(1000));
            _concurrencyManager.FunctionCompleted("testfunction2", TimeSpan.FromMilliseconds(1500));

            status = _concurrencyManager.ConcurrencyStatuses["testfunction2"];
            Assert.Equal(0, status.OutstandingInvocations);
            Assert.Equal(2, status.MaxConcurrentExecutionsSinceLastAdjustment);
            Assert.Equal(2, status.InvocationsSinceLastAdjustment);
            Assert.Equal(2500, status.TotalInvocationTimeSinceLastAdjustmentMs);

            _concurrencyManager.FunctionCompleted("testfunction3", TimeSpan.FromMilliseconds(25));

            status = _concurrencyManager.ConcurrencyStatuses["testfunction3"];
            Assert.Equal(0, status.OutstandingInvocations);
            Assert.Equal(1, status.MaxConcurrentExecutionsSinceLastAdjustment);
            Assert.Equal(1, status.InvocationsSinceLastAdjustment);
            Assert.Equal(25, status.TotalInvocationTimeSinceLastAdjustmentMs);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void FunctionStarted_InvalidFunction_Throws(string functionId)
        {
            var ex = Assert.Throws<ArgumentNullException>(() => _concurrencyManager.FunctionStarted(functionId));
            Assert.Equal(nameof(functionId), ex.ParamName);
        }

        [Fact]
        public void FunctionStarted_DoesNotCreateStatus()
        {
            _concurrencyManager.ConcurrencyStatuses.Clear();
            Assert.Empty(_concurrencyManager.ConcurrencyStatuses);

            _concurrencyManager.FunctionStarted(TestFunctionId);

            Assert.Empty(_concurrencyManager.ConcurrencyStatuses);

            var status = _concurrencyManager.GetStatus(TestFunctionId);
            _concurrencyManager.FunctionStarted(TestFunctionId);
            Assert.Single(_concurrencyManager.ConcurrencyStatuses);
            Assert.Equal(1, status.OutstandingInvocations);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void FunctionCompleted_InvalidFunction_Throws(string functionId)
        {
            var ex = Assert.Throws<ArgumentNullException>(() => _concurrencyManager.FunctionCompleted(functionId, TimeSpan.FromMilliseconds(50)));
            Assert.Equal(nameof(functionId), ex.ParamName);
        }

        [Fact]
        public void FunctionCompleted_DoesNotCreateStatus()
        {
            _concurrencyManager.ConcurrencyStatuses.Clear();
            Assert.Empty(_concurrencyManager.ConcurrencyStatuses);

            _concurrencyManager.FunctionCompleted(TestFunctionId, TimeSpan.FromMilliseconds(10));

            Assert.Empty(_concurrencyManager.ConcurrencyStatuses);

            var status = _concurrencyManager.GetStatus(TestFunctionId);
            _concurrencyManager.FunctionCompleted(TestFunctionId, TimeSpan.FromMilliseconds(10));
            Assert.Single(_concurrencyManager.ConcurrencyStatuses);
            Assert.Equal(0, status.OutstandingInvocations);
        }

        [Fact]
        public void GetSnapshot_ReturnsExpectedResult()
        {
            _concurrencyManager.ConcurrencyStatuses["testfunction1"] = new ConcurrencyStatus("testfunction1", _concurrencyManager)
            {
                CurrentConcurrency = 5
            };
            _concurrencyManager.ConcurrencyStatuses["testfunction2"] = new ConcurrencyStatus("testfunction2", _concurrencyManager)
            {
                CurrentConcurrency = 10
            };
            _concurrencyManager.ConcurrencyStatuses["testfunction3"] = new ConcurrencyStatus("testfunction3", _concurrencyManager)
            {
                CurrentConcurrency = 15
            };

            var snapshot = _concurrencyManager.GetSnapshot();

            Assert.Equal(Utility.GetEffectiveCoresCount(), snapshot.NumberOfCores);

            var functionSnapshot = snapshot.FunctionSnapshots["testfunction1"];
            Assert.Equal(5, functionSnapshot.Concurrency);

            functionSnapshot = snapshot.FunctionSnapshots["testfunction2"];
            Assert.Equal(10, functionSnapshot.Concurrency);

            functionSnapshot = snapshot.FunctionSnapshots["testfunction3"];
            Assert.Equal(15, functionSnapshot.Concurrency);
        }

        [Fact]
        public void ApplySnapshot_PerformsExpectedUpdates()
        {
            var snapshot = new HostConcurrencySnapshot
            {
                Timestamp = DateTime.UtcNow,
                NumberOfCores = Utility.GetEffectiveCoresCount(),
                FunctionSnapshots = new Dictionary<string, FunctionConcurrencySnapshot>
                {
                    { "testfunction1", new FunctionConcurrencySnapshot { Concurrency = 5 } },
                    { "testfunction2", new FunctionConcurrencySnapshot { Concurrency = 10 } },
                    { "testfunction3", new FunctionConcurrencySnapshot { Concurrency = 15 } }
                }
            };

            Assert.Single(_concurrencyManager.ConcurrencyStatuses);
            Assert.Equal(1, _concurrencyManager.ConcurrencyStatuses["testfunction"].CurrentConcurrency);

            _concurrencyManager.ApplySnapshot(snapshot);

            Assert.Equal(4, _concurrencyManager.ConcurrencyStatuses.Count);

            Assert.Equal(1, _concurrencyManager.ConcurrencyStatuses["testfunction"].CurrentConcurrency);
            Assert.Equal(5, _concurrencyManager.ConcurrencyStatuses["testfunction1"].CurrentConcurrency);
            Assert.Equal(10, _concurrencyManager.ConcurrencyStatuses["testfunction2"].CurrentConcurrency);
            Assert.Equal(15, _concurrencyManager.ConcurrencyStatuses["testfunction3"].CurrentConcurrency);
        }

        [Theory]
        [InlineData(100, 8, 4, 50)]
        [InlineData(100, 4, 8, 200)]
        [InlineData(1, 8, 4, 1)]
        [InlineData(1, 4, 8, 2)]
        public void GetCoreAdjustedConcurrency_ReturnsExpectedValue(int concurrency, int otherCores, int cores, int expectedConcurrency)
        {
            Assert.Equal(expectedConcurrency, ConcurrencyManager.GetCoreAdjustedConcurrency(concurrency, otherCores, cores));
        }

        [Fact]
        public void ApplySnapshot_DifferentCoreCount_PerformsExpectedUpdates()
        {
            _concurrencyManager.EffectiveCoresCount = 4;
            int snapshotCoreCount = 8;

            var snapshot = new HostConcurrencySnapshot
            {
                Timestamp = DateTime.UtcNow,
                NumberOfCores = snapshotCoreCount,
                FunctionSnapshots = new Dictionary<string, FunctionConcurrencySnapshot>
                {
                    { "testfunction1", new FunctionConcurrencySnapshot { Concurrency = 50 } },
                    { "testfunction2", new FunctionConcurrencySnapshot { Concurrency = 100 } },
                    { "testfunction3", new FunctionConcurrencySnapshot { Concurrency = 150 } }
                }
            };

            Assert.Single(_concurrencyManager.ConcurrencyStatuses);
            Assert.Equal(1, _concurrencyManager.ConcurrencyStatuses["testfunction"].CurrentConcurrency);

            _concurrencyManager.ApplySnapshot(snapshot);

            Assert.Equal(4, _concurrencyManager.ConcurrencyStatuses.Count);

            // since our core count is half that of the snapshot, we expect the applied concurrency levels
            // to be halved
            Assert.Equal(1, _concurrencyManager.ConcurrencyStatuses["testfunction"].CurrentConcurrency);
            Assert.Equal(25, _concurrencyManager.ConcurrencyStatuses["testfunction1"].CurrentConcurrency);
            Assert.Equal(50, _concurrencyManager.ConcurrencyStatuses["testfunction2"].CurrentConcurrency);
            Assert.Equal(75, _concurrencyManager.ConcurrencyStatuses["testfunction3"].CurrentConcurrency);
        }

        [Fact]
        public void ApplySnapshot_ConcurrencyGreaterThanMax_DoesNotApplySnapshot()
        {
            var snapshot = new HostConcurrencySnapshot
            {
                Timestamp = DateTime.UtcNow,
                NumberOfCores = _concurrencyManager.EffectiveCoresCount,
                FunctionSnapshots = new Dictionary<string, FunctionConcurrencySnapshot>
                {
                    { "testfunction1", new FunctionConcurrencySnapshot { Concurrency = 50 } },
                    { "testfunction2", new FunctionConcurrencySnapshot { Concurrency = 100 } },
                    { "testfunction3", new FunctionConcurrencySnapshot { Concurrency = 501 } }
                }
            };

            Assert.Single(_concurrencyManager.ConcurrencyStatuses);
            Assert.Equal(1, _concurrencyManager.ConcurrencyStatuses["testfunction"].CurrentConcurrency);

            _concurrencyManager.ApplySnapshot(snapshot);

            Assert.Equal(3, _concurrencyManager.ConcurrencyStatuses.Count);

            // snapshot for third function wasn't applied because it's over limit
            Assert.Equal(1, _concurrencyManager.ConcurrencyStatuses["testfunction"].CurrentConcurrency);
            Assert.Equal(50, _concurrencyManager.ConcurrencyStatuses["testfunction1"].CurrentConcurrency);
            Assert.Equal(100, _concurrencyManager.ConcurrencyStatuses["testfunction2"].CurrentConcurrency);
        }

        private void SimulateFunctionInvocations(string functionId, int count)
        {
            for (int i = 0; i < count; i++)
            {
                _concurrencyManager.FunctionStarted(functionId);
                _concurrencyManager.FunctionCompleted(functionId, TimeSpan.FromMilliseconds(10));
            }
        }
    }
}
