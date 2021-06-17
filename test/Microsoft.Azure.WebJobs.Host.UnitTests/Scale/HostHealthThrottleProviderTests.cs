// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
    public class HostHealthThrottleProviderTests
    {
        private readonly HostHealthThrottleProvider _throttleProvider;
        private readonly ILogger _testLogger;

        private HostProcessStatus _status;

        public HostHealthThrottleProviderTests()
        {
            _testLogger = new TestLogger("Test");

            var processMonitorMock = new Mock<IHostProcessMonitor>(MockBehavior.Strict);
            processMonitorMock.Setup(p => p.GetStatus(_testLogger)).Returns(() => _status);

            _throttleProvider = new HostHealthThrottleProvider(processMonitorMock.Object);
        }

        [Theory]
        [InlineData(HostHealthState.Ok, null, ThrottleState.Disabled)]
        [InlineData(HostHealthState.Overloaded, new string[] { "CPU" }, ThrottleState.Enabled)]
        [InlineData(HostHealthState.Overloaded, new string[] { "CPU", "Memory" }, ThrottleState.Enabled)]
        [InlineData(HostHealthState.Unknown, null, ThrottleState.Unknown)]
        public void GetStatus_ReturnsExpectedResult(HostHealthState state, string[] expectedEnabledThrottles, ThrottleState expectedThrottleState)
        {
            _status = new HostProcessStatus
            {
                State = state,
                ExceededLimits = expectedEnabledThrottles
            };

            var status = _throttleProvider.GetStatus(_testLogger);
            Assert.Equal(expectedThrottleState, status.State);
            Assert.Equal(expectedEnabledThrottles, status.EnabledThrottles);
        }
    }
}
