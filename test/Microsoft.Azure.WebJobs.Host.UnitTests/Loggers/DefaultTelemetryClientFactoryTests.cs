﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.ApplicationInsights.SnapshotCollector;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Loggers
{
    public class DefaultTelemetryClientFactoryTests
    {
        [Fact]
        public void InitializeConfiguration_Configures()
        {
            var factory = new DefaultTelemetryClientFactory(string.Empty, null, null);
            var config = factory.InitializeConfiguration();

            // Verify Initializers
            Assert.Equal(3, config.TelemetryInitializers.Count);
            // These will throw if there are not exactly one
            config.TelemetryInitializers.OfType<WebJobsRoleEnvironmentTelemetryInitializer>().Single();
            config.TelemetryInitializers.OfType<WebJobsTelemetryInitializer>().Single();
            config.TelemetryInitializers.OfType<WebJobsSanitizingInitializer>().Single();

            Assert.False(config.TelemetryProcessors.OfType<SnapshotCollectorTelemetryProcessor>().Any());

            // Verify Channel
            Assert.IsType<ServerTelemetryChannel>(config.TelemetryChannel);
        }

        [Fact]
        public void InitializeConfiguration_Configures_SnapshotCollector()
        {
            var factory = new DefaultTelemetryClientFactory(string.Empty, null, new SnapshotCollectorConfiguration(), null);
            var config = factory.InitializeConfiguration();

            // Verify Initializers
            Assert.Equal(3, config.TelemetryInitializers.Count);
            // These will throw if there are not exactly one
            config.TelemetryInitializers.OfType<WebJobsRoleEnvironmentTelemetryInitializer>().Single();
            config.TelemetryInitializers.OfType<WebJobsTelemetryInitializer>().Single();
            config.TelemetryInitializers.OfType<WebJobsSanitizingInitializer>().Single();

            config.TelemetryProcessors.OfType<SnapshotCollectorTelemetryProcessor>().Single();

            // Verify Channel
            Assert.IsType<ServerTelemetryChannel>(config.TelemetryChannel);
        }
    }
}