// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Loggers
{
    public class DefaultTelemetryClientFactoryTests
    {
        private static readonly string InstrumentationKey = "123";

        [Fact]
        public void InitializeConfiguration_Configures()
        {
            var factory = new DefaultTelemetryClientFactory(InstrumentationKey, null, null);
            var config = factory.InitializeConfiguration();

            // Verify Initializers
            Assert.Equal(3, config.TelemetryInitializers.Count);

            Assert.Single(config.TelemetryInitializers.OfType<WebJobsRoleEnvironmentTelemetryInitializer>());
            Assert.Single(config.TelemetryInitializers.OfType<WebJobsTelemetryInitializer>());
            Assert.Single(config.TelemetryInitializers.OfType<WebJobsSanitizingInitializer>());

            // Verify Channel
            Assert.IsType<ServerTelemetryChannel>(config.TelemetryChannel);

            // Verify Active
            Assert.Equal(config.InstrumentationKey, TelemetryConfiguration.Active.InstrumentationKey);

            Assert.Single(TelemetryConfiguration.Active.TelemetryInitializers
                .OfType<WebJobsRoleEnvironmentTelemetryInitializer>());
        }

        [Fact]
        public void InitializeConfiguration_ConfiguresActiveEvenIfIKeySet()
        {
            TelemetryConfiguration.Active.InstrumentationKey = InstrumentationKey;
            var factory = new DefaultTelemetryClientFactory(InstrumentationKey, null, null);
            var config = factory.InitializeConfiguration();

            // Verify Initializers
            Assert.Equal(3, config.TelemetryInitializers.Count);

            Assert.Single(config.TelemetryInitializers.OfType<WebJobsRoleEnvironmentTelemetryInitializer>());
            Assert.Single(config.TelemetryInitializers.OfType<WebJobsTelemetryInitializer>());
            Assert.Single(config.TelemetryInitializers.OfType<WebJobsSanitizingInitializer>());

            // Verify Channel
            Assert.IsType<ServerTelemetryChannel>(config.TelemetryChannel);

            // Verify Active
            Assert.Equal(config.InstrumentationKey, TelemetryConfiguration.Active.InstrumentationKey);

            Assert.Single(TelemetryConfiguration.Active.TelemetryInitializers
                .OfType<WebJobsRoleEnvironmentTelemetryInitializer>());
        }
    }
}
