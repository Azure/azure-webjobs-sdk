// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
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
    public class ThreadPoolStarvationThrottleProviderTests
    {
        private readonly ThreadPoolStarvationThrottleProvider _throttleProvider;
        private readonly LoggerFactory _loggerFactory;
        private readonly TestLoggerProvider _loggerProvider;
        private readonly ILogger _logger;

        public ThreadPoolStarvationThrottleProviderTests()
        {
            _throttleProvider = new ThreadPoolStarvationThrottleProvider();
            _loggerFactory = new LoggerFactory();
            _loggerProvider = new TestLoggerProvider();
            _loggerFactory.AddProvider(_loggerProvider);
            _logger = _loggerFactory.CreateLogger(LogCategories.Concurrency);
        }

        [Fact]
        public async Task GetState_Healthy_ThrottleDisabled()
        {
            for (int i = 0; i < 3; i++)
            {
                await Task.Delay(500);

                var status = _throttleProvider.GetStatus(_logger);
                Assert.Equal(ThrottleState.Disabled, status.State);
                Assert.Null(status.EnabledThrottles);
            }
        }

        [Fact]
        public async Task GetState_Unhealthy_ThrottleEnabled()
        {
            var status = _throttleProvider.GetStatus(_logger);
            Assert.Equal(ThrottleState.Disabled, status.State);
            Assert.Null(status.EnabledThrottles);

            await Task.Delay(500);

            _throttleProvider.ResetInvocations();
            status = _throttleProvider.GetStatus(_logger);
            Assert.Equal(ThrottleState.Enabled, status.State);
            Assert.Single(status.EnabledThrottles, p => p == ThreadPoolStarvationThrottleProvider.ThreadPoolStarvationThrottleName);

            var log = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Warning, log.Level);
            Assert.Equal("Possible thread pool starvation detected.", log.FormattedMessage);
        }
    }
}
