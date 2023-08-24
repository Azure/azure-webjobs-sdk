// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Scale
{
    [Trait(TestTraits.CategoryTraitName, TestTraits.DynamicConcurrency)]
    public class DefaultConcurrencyThrottleManagerTests
    {
        private readonly LoggerFactory _loggerFactory;
        private readonly TestLoggerProvider _loggerProvider;
        private readonly DefaultConcurrencyThrottleManager _throttleManager;
        private ConcurrencyThrottleStatus _throttleProvider1Status;
        private ConcurrencyThrottleStatus _throttleProvider2Status;
        private ConcurrencyThrottleStatus _throttleProvider3Status;

        public DefaultConcurrencyThrottleManagerTests()
        {
            _loggerFactory = new LoggerFactory();
            _loggerProvider = new TestLoggerProvider();
            _loggerFactory.AddProvider(_loggerProvider);
            var logger = _loggerFactory.CreateLogger(LogCategories.Concurrency);

            var mockThrottleProvider1 = new Mock<IConcurrencyThrottleProvider>(MockBehavior.Strict);
            mockThrottleProvider1.Setup(p => p.GetStatus(logger)).Returns(() => _throttleProvider1Status);

            var mockThrottleProvider2 = new Mock<IConcurrencyThrottleProvider>(MockBehavior.Strict);
            mockThrottleProvider2.Setup(p => p.GetStatus(logger)).Returns(() => _throttleProvider2Status);

            var mockThrottleProvider3 = new Mock<IConcurrencyThrottleProvider>(MockBehavior.Strict);
            mockThrottleProvider3.Setup(p => p.GetStatus(logger)).Returns(() => _throttleProvider3Status);

            var throttleProviders = new IConcurrencyThrottleProvider[] { mockThrottleProvider1.Object, mockThrottleProvider2.Object, mockThrottleProvider3.Object };
            _throttleManager = new DefaultConcurrencyThrottleManager(throttleProviders, _loggerFactory);
        }

        [Fact]
        public void GetStatus_ThrottleEnabled_ReturnsExpectedResult()
        {
            TestHelpers.SetupStopwatch(_throttleManager.LastThrottleCheckStopwatch, TimeSpan.FromSeconds(5));
            var lastThrottleCheck = _throttleManager.LastThrottleCheckStopwatch.Elapsed;

            _throttleProvider1Status = new ConcurrencyThrottleStatus { State = ThrottleState.Disabled };
            _throttleProvider2Status = new ConcurrencyThrottleStatus { State = ThrottleState.Enabled, EnabledThrottles = new List<string> { "Test" }.AsReadOnly() };
            _throttleProvider3Status = new ConcurrencyThrottleStatus { State = ThrottleState.Disabled };

            var status = _throttleManager.GetStatus();
            Assert.Equal(ThrottleState.Enabled, status.State);
            Assert.Equal(0, status.ConsecutiveCount);
            Assert.Single(status.EnabledThrottles, p => p == "Test");
            Assert.True(_throttleManager.LastThrottleCheckStopwatch.Elapsed < lastThrottleCheck);
        }

        [Fact]
        public void GetStatus_MultipleThrottlesEnabled_ReturnsExpectedResult()
        {
            TestHelpers.SetupStopwatch(_throttleManager.LastThrottleCheckStopwatch, TimeSpan.FromSeconds(5));
            var lastThrottleCheck = _throttleManager.LastThrottleCheckStopwatch.Elapsed;

            _throttleProvider1Status = new ConcurrencyThrottleStatus { State = ThrottleState.Disabled };
            _throttleProvider2Status = new ConcurrencyThrottleStatus { State = ThrottleState.Enabled, EnabledThrottles = new List<string> { "Throttle1" }.AsReadOnly() };
            _throttleProvider3Status = new ConcurrencyThrottleStatus { State = ThrottleState.Enabled, EnabledThrottles = new List<string> { "Throttle2", "Throttle3" }.AsReadOnly() };

            var status = _throttleManager.GetStatus();
            Assert.Equal(ThrottleState.Enabled, status.State);
            Assert.Equal(0, status.ConsecutiveCount);
            Assert.Equal(3, status.EnabledThrottles.Count);
            Assert.Collection(status.EnabledThrottles,
                p => Assert.Equal(p, "Throttle1"),
                p => Assert.Equal(p, "Throttle2"),
                p => Assert.Equal(p, "Throttle3"));
            Assert.True(_throttleManager.LastThrottleCheckStopwatch.Elapsed < lastThrottleCheck);
        }

        [Fact]
        public void GetStatus_ThrottleEnabled_ThrottleUnknown_ReturnsExpectedResult()
        {
            TestHelpers.SetupStopwatch(_throttleManager.LastThrottleCheckStopwatch, TimeSpan.FromSeconds(5));
            var lastThrottleCheck = _throttleManager.LastThrottleCheckStopwatch.Elapsed;

            _throttleProvider1Status = new ConcurrencyThrottleStatus { State = ThrottleState.Disabled };
            _throttleProvider2Status = new ConcurrencyThrottleStatus { State = ThrottleState.Enabled, EnabledThrottles = new List<string> { "Test" }.AsReadOnly() };
            _throttleProvider3Status = new ConcurrencyThrottleStatus { State = ThrottleState.Unknown };

            var status = _throttleManager.GetStatus();
            Assert.Equal(ThrottleState.Enabled, status.State);
            Assert.Equal(0, status.ConsecutiveCount);
            Assert.Single(status.EnabledThrottles, p => p == "Test");
            Assert.True(_throttleManager.LastThrottleCheckStopwatch.Elapsed < lastThrottleCheck);
        }

        [Fact]
        public void GetStatus_ThrottleDisabled_ReturnsExpectedResult()
        {
            TestHelpers.SetupStopwatch(_throttleManager.LastThrottleCheckStopwatch, TimeSpan.FromSeconds(5));
            var lastThrottleCheck = _throttleManager.LastThrottleCheckStopwatch.Elapsed;

            _throttleProvider1Status = new ConcurrencyThrottleStatus { State = ThrottleState.Disabled };
            _throttleProvider2Status = new ConcurrencyThrottleStatus { State = ThrottleState.Disabled };
            _throttleProvider3Status = new ConcurrencyThrottleStatus { State = ThrottleState.Disabled };

            var status = _throttleManager.GetStatus();
            Assert.Equal(ThrottleState.Disabled, status.State);
            Assert.Equal(0, status.ConsecutiveCount);
            Assert.Null(status.EnabledThrottles);
            Assert.True(_throttleManager.LastThrottleCheckStopwatch.Elapsed < lastThrottleCheck);
        }

        [Fact]
        public void GetStatus_ThrottleUnknown_ReturnsExpectedResult()
        {
            TestHelpers.SetupStopwatch(_throttleManager.LastThrottleCheckStopwatch, TimeSpan.FromSeconds(5));
            var lastThrottleCheck = _throttleManager.LastThrottleCheckStopwatch.Elapsed;

            _throttleProvider1Status = new ConcurrencyThrottleStatus { State = ThrottleState.Disabled };
            _throttleProvider2Status = new ConcurrencyThrottleStatus { State = ThrottleState.Unknown };
            _throttleProvider3Status = new ConcurrencyThrottleStatus { State = ThrottleState.Disabled };

            var status = _throttleManager.GetStatus();
            Assert.Equal(ThrottleState.Unknown, status.State);
            Assert.Equal(1, status.ConsecutiveCount);
            Assert.Null(status.EnabledThrottles);
            Assert.True(_throttleManager.LastThrottleCheckStopwatch.Elapsed < lastThrottleCheck);
        }

        [Fact]
        public async Task GetStatus_ThrottlesUpdates()
        {
            TestHelpers.SetupStopwatch(_throttleManager.LastThrottleCheckStopwatch, TimeSpan.FromSeconds(5));
            TimeSpan lastThrottleCheck = _throttleManager.LastThrottleCheckStopwatch.Elapsed;

            _throttleProvider1Status = new ConcurrencyThrottleStatus { State = ThrottleState.Disabled };
            _throttleProvider2Status = new ConcurrencyThrottleStatus { State = ThrottleState.Enabled, EnabledThrottles = new List<string> { "Test" }.AsReadOnly() };
            _throttleProvider3Status = new ConcurrencyThrottleStatus { State = ThrottleState.Disabled };

            var status = _throttleManager.GetStatus();
            Assert.Equal(ThrottleState.Enabled, status.State);
            Assert.Equal(0, status.ConsecutiveCount);
            Assert.Single(status.EnabledThrottles, p => p == "Test");
            Assert.True(_throttleManager.LastThrottleCheckStopwatch.Elapsed < lastThrottleCheck);

            lastThrottleCheck = _throttleManager.LastThrottleCheckStopwatch.Elapsed;

            // now make a bunch of rapid requests - we shouldn't query providers
            // we should return the last result
            for (int i = 0; i < 5; i++)
            {
                status = _throttleManager.GetStatus();
                Assert.Equal(ThrottleState.Enabled, status.State);
                Assert.Equal(0, status.ConsecutiveCount);
                Assert.Single(status.EnabledThrottles, p => p == "Test");
                Assert.True(_throttleManager.LastThrottleCheckStopwatch.Elapsed > lastThrottleCheck);

                await Task.Delay(100);
            }

            // simulate time moving forward
            TestHelpers.SetupStopwatch(_throttleManager.LastThrottleCheckStopwatch, TimeSpan.FromSeconds(5));
            lastThrottleCheck = _throttleManager.LastThrottleCheckStopwatch.Elapsed;

            status = _throttleManager.GetStatus();
            Assert.Equal(ThrottleState.Enabled, status.State);
            Assert.Equal(1, status.ConsecutiveCount);
            Assert.Single(status.EnabledThrottles, p => p == "Test");
            Assert.True(_throttleManager.LastThrottleCheckStopwatch.Elapsed < lastThrottleCheck);
        }

        [Fact]
        public void GetStatus_ThrottleStateRun()
        {
            _throttleProvider1Status = new ConcurrencyThrottleStatus { State = ThrottleState.Disabled };
            _throttleProvider2Status = new ConcurrencyThrottleStatus { State = ThrottleState.Enabled, EnabledThrottles = new List<string> { "Test" }.AsReadOnly() };
            _throttleProvider3Status = new ConcurrencyThrottleStatus { State = ThrottleState.Disabled };

            ConcurrencyThrottleAggregateStatus status;
            for (int i = 0; i < 5; i++)
            {
                TestHelpers.SetupStopwatch(_throttleManager.LastThrottleCheckStopwatch, TimeSpan.FromMilliseconds(1100));
                status = _throttleManager.GetStatus();
                Assert.Equal(ThrottleState.Enabled, status.State);
                Assert.Single(status.EnabledThrottles, p => p == "Test");
                Assert.Equal(i, status.ConsecutiveCount);
            }

            // now break the run
            _throttleProvider2Status = new ConcurrencyThrottleStatus { State = ThrottleState.Disabled };

            TestHelpers.SetupStopwatch(_throttleManager.LastThrottleCheckStopwatch, TimeSpan.FromMilliseconds(1100));
            status = _throttleManager.GetStatus();
            Assert.Equal(ThrottleState.Disabled, status.State);
            Assert.Null(status.EnabledThrottles);
            Assert.Equal(0, status.ConsecutiveCount);
        }
    }
}
