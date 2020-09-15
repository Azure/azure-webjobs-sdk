// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Loggers
{
    public class DefaultTelemetryClientFactoryTests
    {
        [Fact]
        public void InitializeConfiguguration_Configures()
        {
            var factory = new DefaultTelemetryClientFactory(string.Empty, null, null);
            var config = factory.InitializeConfiguration();

            // Verify Initializers
            Assert.Equal(3, config.TelemetryInitializers.Count);
            // These will throw if there are not exactly one
            config.TelemetryInitializers.OfType<WebJobsRoleEnvironmentTelemetryInitializer>().Single();
            config.TelemetryInitializers.OfType<WebJobsTelemetryInitializer>().Single();
            config.TelemetryInitializers.OfType<WebJobsSanitizingInitializer>().Single();

            // Verify Channel
            Assert.IsType<ServerTelemetryChannel>(config.TelemetryChannel);
        }

        [Fact]
        public void InitializeConfiguration_ConfiguresWithIngestionEndpoint()
        {
            var testIngestionEndpoint = "https://testIngestion.Endpoint/";
            var factory = new DefaultTelemetryClientFactory(string.Empty, null, testIngestionEndpoint, null, null);
            var config = factory.InitializeConfiguration();

            Assert.Equal(testIngestionEndpoint.ToLower(), config.TelemetryChannel.EndpointAddress.ToLower());
        }
    }
}
