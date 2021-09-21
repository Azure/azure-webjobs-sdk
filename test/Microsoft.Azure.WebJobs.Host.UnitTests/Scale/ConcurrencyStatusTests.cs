// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Scale
{
    [Trait(TestTraits.CategoryTraitName, TestTraits.DynamicConcurrency)]
    public class ConcurrencyStatusTests
    {
        private const string TestFunctionId = "testfunction";
        private const int TestMaxConcurrency = 500;

        private readonly Random _rand = new Random();
        private readonly Mock<ConcurrencyManager> _concurrencyManagerMock;
        private readonly ConcurrencyStatus _testConcurrencyStatus;
        private readonly LoggerFactory _loggerFactory;
        private readonly TestLoggerProvider _loggerProvider;
        private readonly ILogger _testLogger;

        private ConcurrencyThrottleAggregateStatus _throttleStatus;

        public ConcurrencyStatusTests()
        {
            _throttleStatus = new ConcurrencyThrottleAggregateStatus();

            _concurrencyManagerMock = new Mock<ConcurrencyManager>(MockBehavior.Strict);
            _concurrencyManagerMock.SetupGet(p => p.ThrottleStatus).Returns(() => _throttleStatus);

            _testConcurrencyStatus = new ConcurrencyStatus(TestFunctionId, _concurrencyManagerMock.Object);

            _loggerFactory = new LoggerFactory();
            _loggerProvider = new TestLoggerProvider();
            _loggerFactory.AddProvider(_loggerProvider);
            _testLogger = _loggerFactory.CreateLogger("Test");
        }

        [Fact]
        public void FunctionId_ReturnsExpectedValue()
        {
            Assert.Equal(TestFunctionId, _testConcurrencyStatus.FunctionId);
        }

        [Fact]
        public void GetAvailableInvocationCount_ReturnsAvailableBuffer()
        {
            _testConcurrencyStatus.OutstandingInvocations = 25;
            _testConcurrencyStatus.CurrentConcurrency = 100;

            Assert.Equal(75, _testConcurrencyStatus.GetAvailableInvocationCount(0));
            Assert.Equal(15, _testConcurrencyStatus.GetAvailableInvocationCount(85));
        }

        [Fact]
        public void GetAvailableInvocationCount_NegativePendingInvocations_Throws()
        {
            _testConcurrencyStatus.OutstandingInvocations = 75;
            _testConcurrencyStatus.CurrentConcurrency = 100;

            Assert.Throws<ArgumentOutOfRangeException>(() => _testConcurrencyStatus.GetAvailableInvocationCount(-1));
        }

        [Fact]
        public void GetAvailableInvocationCount_OverLimit_ReturnsZero()
        {
            _testConcurrencyStatus.OutstandingInvocations = 105;
            _testConcurrencyStatus.CurrentConcurrency = 100;

            Assert.Equal(0, _testConcurrencyStatus.GetAvailableInvocationCount(0));
            Assert.Equal(0, _testConcurrencyStatus.GetAvailableInvocationCount(120));
        }

        [Fact]
        public void GetAvailableInvocationCount_ThrottleEnabled_ReturnsZero()
        {
            _testConcurrencyStatus.OutstandingInvocations = 25;
            _testConcurrencyStatus.CurrentConcurrency = 100;
            _throttleStatus = new ConcurrencyThrottleAggregateStatus
            {
                State = ThrottleState.Enabled,
                EnabledThrottles = new List<string> { "CPU" }
            };

            Assert.Equal(0, _testConcurrencyStatus.GetAvailableInvocationCount(0));
        }

        [Fact]
        public void ApplySnapshot_PerformsExpectedUpdates()
        {
            var snapshot = new FunctionConcurrencySnapshot
            {
                Concurrency = 50
            };
            Assert.Equal(1, _testConcurrencyStatus.CurrentConcurrency);

            _testConcurrencyStatus.ApplySnapshot(snapshot);
            Assert.Equal(50, _testConcurrencyStatus.CurrentConcurrency);

            // a snapshot concurrency whose value is lower is not applied
            snapshot.Concurrency = 10;
            _testConcurrencyStatus.ApplySnapshot(snapshot);
            Assert.Equal(50, _testConcurrencyStatus.CurrentConcurrency);
        }

        [Fact]
        public void GetLatencyAdjustedInterval_ReturnsExpectedValue()
        {
            // faster functions should have shorter adjustment intervals
            // this function averages 100ms per invocation
            _testConcurrencyStatus.TotalInvocationTimeSinceLastAdjustmentMs = 2000;
            _testConcurrencyStatus.InvocationsSinceLastAdjustment = 20;
            TimeSpan interval = _testConcurrencyStatus.GetLatencyAdjustedInterval(TimeSpan.FromMilliseconds(2000), TimeSpan.FromMilliseconds(5000), 1);
            Assert.Equal(2100, interval.TotalMilliseconds);

            // if we haven't had any invocations to compute a latency, we
            // return the default interval
            _testConcurrencyStatus.InvocationsSinceLastAdjustment = 0;
            interval = _testConcurrencyStatus.GetLatencyAdjustedInterval(TimeSpan.FromMilliseconds(2000), TimeSpan.FromMilliseconds(5000), 1);
            Assert.Equal(5000, interval.TotalMilliseconds);

            // a longer running function that exceeds the max interval is
            // capped to max
            _testConcurrencyStatus.TotalInvocationTimeSinceLastAdjustmentMs = 30000;
            _testConcurrencyStatus.InvocationsSinceLastAdjustment = 5;
            interval = _testConcurrencyStatus.GetLatencyAdjustedInterval(TimeSpan.FromMilliseconds(2000), TimeSpan.FromMilliseconds(5000), 1);
            Assert.Equal(5000, interval.TotalMilliseconds);
        }

        [Fact]
        public void CanAdjustConcurrency_ReturnsExpectedValue()
        {
            TestHelpers.SetupStopwatch(_testConcurrencyStatus.LastConcurrencyAdjustmentStopwatch, TimeSpan.FromMilliseconds(3000));
            _testConcurrencyStatus.TotalInvocationTimeSinceLastAdjustmentMs = 100;
            _testConcurrencyStatus.InvocationsSinceLastAdjustment = 1;
            bool canAdjust = _testConcurrencyStatus.CanAdjustConcurrency();
            Assert.True(canAdjust);

            TestHelpers.SetupStopwatch(_testConcurrencyStatus.LastConcurrencyAdjustmentStopwatch, TimeSpan.FromMilliseconds(500));
            canAdjust = _testConcurrencyStatus.CanAdjustConcurrency();
            Assert.False(canAdjust);

            // no invocations to compute a latency based interval, so the default is used
            TestHelpers.SetupStopwatch(_testConcurrencyStatus.LastConcurrencyAdjustmentStopwatch, TimeSpan.FromMilliseconds(5001));
            _testConcurrencyStatus.TotalInvocationTimeSinceLastAdjustmentMs = 0;
            _testConcurrencyStatus.InvocationsSinceLastAdjustment = 0;
            canAdjust = _testConcurrencyStatus.CanAdjustConcurrency();
            Assert.True(canAdjust);

            TestHelpers.SetupStopwatch(_testConcurrencyStatus.LastConcurrencyAdjustmentStopwatch, TimeSpan.FromMilliseconds(4000));
            canAdjust = _testConcurrencyStatus.CanAdjustConcurrency();
            Assert.False(canAdjust);
        }

        [Theory]
        [InlineData(50, true)]
        [InlineData(1, false)]
        public void CanDecreaseConcurrency_ReturnsExpectedValue(int concurrency, bool expected)
        {
            _testConcurrencyStatus.CurrentConcurrency = concurrency;
            Assert.Equal(expected, _testConcurrencyStatus.CanDecreaseConcurrency());
        }

        [Fact]
        public void CanIncreaseConcurrency_ReturnsExpectedValue()
        {
            _testConcurrencyStatus.CurrentConcurrency = 500;

            // we've had to decrease concurrency recently, so we expect false
            TestHelpers.SetupStopwatch(_testConcurrencyStatus.LastConcurrencyDecreaseStopwatch, TimeSpan.FromSeconds(20));
            _testConcurrencyStatus.MaxConcurrentExecutionsSinceLastAdjustment = 0;
            _testConcurrencyStatus.TotalInvocationTimeSinceLastAdjustmentMs = 0;
            _testConcurrencyStatus.InvocationsSinceLastAdjustment = 0;
            bool canAdjust = _testConcurrencyStatus.CanIncreaseConcurrency(TestMaxConcurrency);
            Assert.False(canAdjust);

            // we've had invocations, but our latency adjusted window is still too small
            TestHelpers.SetupStopwatch(_testConcurrencyStatus.LastConcurrencyDecreaseStopwatch, TimeSpan.FromSeconds(14));
            _testConcurrencyStatus.MaxConcurrentExecutionsSinceLastAdjustment = 1;
            _testConcurrencyStatus.TotalInvocationTimeSinceLastAdjustmentMs = 500;
            _testConcurrencyStatus.InvocationsSinceLastAdjustment = 1;
            canAdjust = _testConcurrencyStatus.CanIncreaseConcurrency(TestMaxConcurrency);
            Assert.False(canAdjust);

            // we satisfy the quiet period window, but we're not utilizing our current concurrency level
            TestHelpers.SetupStopwatch(_testConcurrencyStatus.LastConcurrencyDecreaseStopwatch, TimeSpan.FromSeconds(16));
            canAdjust = _testConcurrencyStatus.CanIncreaseConcurrency(TestMaxConcurrency);
            Assert.False(canAdjust);

            // we satisfy the quiet period window, but we're not utilizing our current concurrency level
            TestHelpers.SetupStopwatch(_testConcurrencyStatus.LastConcurrencyDecreaseStopwatch, TimeSpan.FromSeconds(16));
            canAdjust = _testConcurrencyStatus.CanIncreaseConcurrency(TestMaxConcurrency);
            Assert.False(canAdjust);

            // we've utilized the full current concurrency level, but can't increase because we're at the
            // max concurrency level
            TestHelpers.SetupStopwatch(_testConcurrencyStatus.LastConcurrencyDecreaseStopwatch, TimeSpan.FromSeconds(16));
            _testConcurrencyStatus.MaxConcurrentExecutionsSinceLastAdjustment = 500;
            canAdjust = _testConcurrencyStatus.CanIncreaseConcurrency(TestMaxConcurrency);
            Assert.False(canAdjust);

            // all conditions satisfied so we can increase
            TestHelpers.SetupStopwatch(_testConcurrencyStatus.LastConcurrencyDecreaseStopwatch, TimeSpan.FromSeconds(16));
            _testConcurrencyStatus.CurrentConcurrency = 10;
            _testConcurrencyStatus.MaxConcurrentExecutionsSinceLastAdjustment = 10;
            canAdjust = _testConcurrencyStatus.CanIncreaseConcurrency(TestMaxConcurrency);
            Assert.True(canAdjust);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(-1)]
        public void GetNextAdjustment_RunInSameDirection_ReturnsExpectedValues(int direction)
        {
            List<int> adjustments = new List<int>();

            int delta;
            for (int i = 0; i < 10; i++)
            {
                _testConcurrencyStatus.LastConcurrencyAdjustmentStopwatch.Restart();
                delta = _testConcurrencyStatus.GetNextAdjustment(direction);
                adjustments.Add(delta);
            }

            Assert.Equal(new int[] { 1, 2, 3, 4, 5, 6, 6, 6, 6, 6 }.Select(p => direction * p), adjustments);

            // if too much time passes, the run window ends and we start back at 1
            TestHelpers.SetupStopwatch(_testConcurrencyStatus.LastConcurrencyAdjustmentStopwatch, TimeSpan.FromSeconds(11));
            delta = _testConcurrencyStatus.GetNextAdjustment(1);
            Assert.Equal(1, delta);
        }

        [Fact]
        public void GetNextAdjustment_DirectionChange_EndsRun()
        {
            List<int> adjustments = new List<int>();

            int delta;
            for (int i = 0; i < 5; i++)
            {
                _testConcurrencyStatus.LastConcurrencyAdjustmentStopwatch.Restart();
                delta = _testConcurrencyStatus.GetNextAdjustment(1);
                adjustments.Add(delta);
            }

            Assert.Equal(adjustments, new int[] { 1, 2, 3, 4, 5 });

            // if we switch directions, the run ends
            delta = _testConcurrencyStatus.GetNextAdjustment(-1);
            Assert.Equal(-1, delta);
        }

        [Fact]
        public void IncreaseConcurrency_PerformsExpectedAdjustments()
        {
            // initialize some values - we expect these to be reset below
            _testConcurrencyStatus.MaxConcurrentExecutionsSinceLastAdjustment = 25;
            _testConcurrencyStatus.InvocationsSinceLastAdjustment = 50;
            _testConcurrencyStatus.TotalInvocationTimeSinceLastAdjustmentMs = 30000;

            Assert.False(_testConcurrencyStatus.LastConcurrencyDecreaseStopwatch.IsRunning);
            Assert.True(_testConcurrencyStatus.LastConcurrencyAdjustmentStopwatch.IsRunning);

            var lastAdjustment = _testConcurrencyStatus.LastConcurrencyAdjustmentStopwatch.Elapsed;

            Assert.Equal(1, _testConcurrencyStatus.CurrentConcurrency);

            _testConcurrencyStatus.IncreaseConcurrency();
            Assert.Equal(2, _testConcurrencyStatus.CurrentConcurrency);

            Assert.Equal(0, _testConcurrencyStatus.MaxConcurrentExecutionsSinceLastAdjustment);
            Assert.Equal(0, _testConcurrencyStatus.InvocationsSinceLastAdjustment);
            Assert.Equal(0, _testConcurrencyStatus.TotalInvocationTimeSinceLastAdjustmentMs);

            // expect the adjustment stopwatch to restart
            Assert.False(_testConcurrencyStatus.LastConcurrencyDecreaseStopwatch.IsRunning);
            Assert.True(_testConcurrencyStatus.LastConcurrencyAdjustmentStopwatch.IsRunning);
            Assert.True(_testConcurrencyStatus.LastConcurrencyAdjustmentStopwatch.Elapsed < lastAdjustment);
        }

        [Fact]
        public void DecreaseConcurrency_PerformsExpectedAdjustments()
        {
            // initialize some values - we expect these to be reset below
            _testConcurrencyStatus.MaxConcurrentExecutionsSinceLastAdjustment = 25;
            _testConcurrencyStatus.InvocationsSinceLastAdjustment = 50;
            _testConcurrencyStatus.TotalInvocationTimeSinceLastAdjustmentMs = 30000;
            _testConcurrencyStatus.CurrentConcurrency = 5;

            Assert.False(_testConcurrencyStatus.LastConcurrencyDecreaseStopwatch.IsRunning);
            Assert.True(_testConcurrencyStatus.LastConcurrencyAdjustmentStopwatch.IsRunning);

            TimeSpan lastDecrease = _testConcurrencyStatus.LastConcurrencyDecreaseStopwatch.Elapsed;
            TimeSpan lastAdjustment = _testConcurrencyStatus.LastConcurrencyAdjustmentStopwatch.Elapsed;

            _testConcurrencyStatus.DecreaseConcurrency();
            Assert.Equal(4, _testConcurrencyStatus.CurrentConcurrency);

            Assert.Equal(0, _testConcurrencyStatus.MaxConcurrentExecutionsSinceLastAdjustment);
            Assert.Equal(0, _testConcurrencyStatus.InvocationsSinceLastAdjustment);
            Assert.Equal(0, _testConcurrencyStatus.TotalInvocationTimeSinceLastAdjustmentMs);

            // expect both stopwatches to restart
            Assert.True(_testConcurrencyStatus.LastConcurrencyDecreaseStopwatch.IsRunning);
            Assert.True(_testConcurrencyStatus.LastConcurrencyAdjustmentStopwatch.IsRunning);
            Assert.True(_testConcurrencyStatus.LastConcurrencyDecreaseStopwatch.Elapsed > lastDecrease);
            Assert.True(_testConcurrencyStatus.LastConcurrencyAdjustmentStopwatch.Elapsed < lastAdjustment);
        }

        [Fact]
        public void IncreaseConcurrency_Run_EndsWithExpectedResult()
        {
            Assert.Equal(1, _testConcurrencyStatus.CurrentConcurrency);

            for (int i = 0; i < 10; i++)
            {
                _testConcurrencyStatus.IncreaseConcurrency();
            }

            Assert.Equal(46, _testConcurrencyStatus.CurrentConcurrency);
        }

        [Fact]
        public void DecreaseConcurrency_Run_EndsWithExpectedResult()
        {
            _testConcurrencyStatus.CurrentConcurrency = 50;

            for (int i = 0; i < 5; i++)
            {
                _testConcurrencyStatus.DecreaseConcurrency();
            }

            Assert.Equal(35, _testConcurrencyStatus.CurrentConcurrency);
        }

        [Fact]
        public void FunctionInvocationTracking_MaintainsExpectedCounts()
        {
            Assert.Equal(0, _testConcurrencyStatus.OutstandingInvocations);
            Assert.Equal(0, _testConcurrencyStatus.InvocationsSinceLastAdjustment);
            Assert.Equal(0, _testConcurrencyStatus.MaxConcurrentExecutionsSinceLastAdjustment);

            _testConcurrencyStatus.FunctionStarted();
            Assert.Equal(1, _testConcurrencyStatus.OutstandingInvocations);
            Assert.Equal(0, _testConcurrencyStatus.InvocationsSinceLastAdjustment);
            Assert.Equal(1, _testConcurrencyStatus.MaxConcurrentExecutionsSinceLastAdjustment);

            _testConcurrencyStatus.FunctionStarted();
            Assert.Equal(2, _testConcurrencyStatus.OutstandingInvocations);
            Assert.Equal(0, _testConcurrencyStatus.InvocationsSinceLastAdjustment);
            Assert.Equal(2, _testConcurrencyStatus.MaxConcurrentExecutionsSinceLastAdjustment);

            _testConcurrencyStatus.FunctionCompleted(TimeSpan.FromMilliseconds(25));
            Assert.Equal(1, _testConcurrencyStatus.OutstandingInvocations);
            Assert.Equal(1, _testConcurrencyStatus.InvocationsSinceLastAdjustment);
            Assert.Equal(2, _testConcurrencyStatus.MaxConcurrentExecutionsSinceLastAdjustment);
            Assert.Equal(25, _testConcurrencyStatus.TotalInvocationTimeSinceLastAdjustmentMs);

            _testConcurrencyStatus.FunctionCompleted(TimeSpan.FromMilliseconds(25));
            Assert.Equal(0, _testConcurrencyStatus.OutstandingInvocations);
            Assert.Equal(2, _testConcurrencyStatus.InvocationsSinceLastAdjustment);
            Assert.Equal(2, _testConcurrencyStatus.MaxConcurrentExecutionsSinceLastAdjustment);
            Assert.Equal(50, _testConcurrencyStatus.TotalInvocationTimeSinceLastAdjustmentMs);
        }

        [Fact]
        public async Task FunctionExecutionEvents_Synchronization()
        {
            int numExecutionsPerThread = 250;
            int numThreads = 20;
            int expectedInvocationCount = numExecutionsPerThread * numThreads;

            List<Task> tasks = new List<Task>();
            ConcurrentBag<int> latencies = new ConcurrentBag<int>();
            object syncLock = new object();
            int maxConcurrentExecutions = 0;
            int outstandingExecutions = 0;
            for (int i = 0; i < numThreads; i++)
            {
                var task = Task.Run(async () =>
                {
                    for (int j = 0; j < numExecutionsPerThread; j++)
                    {
                        lock (syncLock)
                        {
                            outstandingExecutions++;

                            if (outstandingExecutions > maxConcurrentExecutions)
                            {
                                maxConcurrentExecutions = outstandingExecutions;
                            }
                        }

                        _testConcurrencyStatus.FunctionStarted();

                        int latency = _rand.Next(5, 25);
                        latencies.Add(latency);
                        await Task.Delay(latency);

                        lock (syncLock)
                        {
                            outstandingExecutions--;
                        }

                        _testConcurrencyStatus.FunctionCompleted(TimeSpan.FromMilliseconds(latency));
                    }
                });
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);

            Assert.Equal(0, _testConcurrencyStatus.OutstandingInvocations);
            Assert.Equal(expectedInvocationCount, _testConcurrencyStatus.InvocationsSinceLastAdjustment);
            Assert.Equal(latencies.Sum(), _testConcurrencyStatus.TotalInvocationTimeSinceLastAdjustmentMs);
            Assert.Equal(numThreads, _testConcurrencyStatus.MaxConcurrentExecutionsSinceLastAdjustment);
        }

        [Fact]
        public void LogStatus_OnlyLogsWhenStatusChanges()
        {
            _testConcurrencyStatus.LogUpdates(_testLogger);
            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            Assert.Single(logs);
            Assert.Equal("testfunction Concurrency: 1, OutstandingInvocations: 0", logs[0].FormattedMessage);

            _loggerProvider.ClearAllLogMessages();
            _testConcurrencyStatus.LogUpdates(_testLogger);
            _testConcurrencyStatus.LogUpdates(_testLogger);
            logs = _loggerProvider.GetAllLogMessages().ToArray();
            Assert.Empty(logs);

            _loggerProvider.ClearAllLogMessages();
            _testConcurrencyStatus.IncreaseConcurrency();
            _testConcurrencyStatus.LogUpdates(_testLogger);
            _testConcurrencyStatus.LogUpdates(_testLogger);
            logs = _loggerProvider.GetAllLogMessages().ToArray();
            Assert.Single(logs);
            Assert.Equal("testfunction Concurrency: 2, OutstandingInvocations: 0", logs[0].FormattedMessage);

            _loggerProvider.ClearAllLogMessages();
            _testConcurrencyStatus.ApplySnapshot(new FunctionConcurrencySnapshot { Concurrency = 3 });
            _testConcurrencyStatus.LogUpdates(_testLogger);
            _testConcurrencyStatus.LogUpdates(_testLogger);
            logs = _loggerProvider.GetAllLogMessages().ToArray();
            Assert.Single(logs);
            Assert.Equal("testfunction Concurrency: 3, OutstandingInvocations: 0", logs[0].FormattedMessage);
        }
    }
}
