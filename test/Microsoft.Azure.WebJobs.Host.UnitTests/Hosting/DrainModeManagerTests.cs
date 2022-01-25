// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Hosting
{
    public class DrainModeManagerTests
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<DrainModeManager> _logger;
        private readonly TestLoggerProvider _loggerProvider;

        public DrainModeManagerTests()
        {
            _loggerFactory = new LoggerFactory();
            _loggerProvider = new TestLoggerProvider();
            _loggerFactory.AddProvider(_loggerProvider);
            _logger = _loggerFactory.CreateLogger<DrainModeManager>();
        }

        [Fact]
        public void RegisterListener_AddsToListenerCollection()
        {
            Mock<IListener> listener = new Mock<IListener>(MockBehavior.Strict);
            var drainModeManager = new DrainModeManager(_logger);
            drainModeManager.RegisterListener(listener.Object);

            Assert.Equal(drainModeManager.Listeners.ElementAt(0), listener.Object);
        }

        [Fact]
        public async Task RegisterListener_EnableDrainModeAsync_CallsStopAsyncAndEnablesDrainMode()
        {
            Mock<IListener> listener = new Mock<IListener>(MockBehavior.Strict);
            listener.Setup(bl => bl.StopAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));

            var drainModeManager = new DrainModeManager(_logger);
            drainModeManager.RegisterListener(listener.Object);

            await drainModeManager.EnableDrainModeAsync(CancellationToken.None);
            listener.VerifyAll();

            Assert.Equal(drainModeManager.IsDrainModeEnabled, true);

            Assert.Collection(_loggerProvider.GetAllLogMessages().Select(p => p.FormattedMessage),
               p => Assert.Equal("DrainMode mode enabled", p),
               p => Assert.Equal("Calling StopAsync on the registered listeners", p),
               p => Assert.Equal("Call to StopAsync complete, registered listeners are now stopped", p));
        }
    }
}