// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Loggers
{
    public class WebJobsRoleEnvironmentTelemetryInitializerTests : IDisposable
    {
        public WebJobsRoleEnvironmentTelemetryInitializerTests()
        {
            // make sure these are clear before each test
            SetEnvironmentVariables(null, null);
        }

        [Fact]
        public void Initialize_DoesNotThrow_WhenNoEnvironmentVariables()
        {
            var initializer = new WebJobsRoleEnvironmentTelemetryInitializer();

            var telemetry = new TraceTelemetry();
            initializer.Initialize(telemetry);

            Assert.Null(telemetry.Context.Cloud.RoleName);
            Assert.Null(telemetry.Context.GetInternalContext().NodeName);
        }

        [Fact]
        public void Initialize_WithSlot()
        {
            SetEnvironmentVariables("mytestsite", "Staging");

            var initializer = new WebJobsRoleEnvironmentTelemetryInitializer();

            var telemetry = new TraceTelemetry();
            initializer.Initialize(telemetry);

            Assert.Equal("mytestsite-staging", telemetry.Context.Cloud.RoleName);
            Assert.Equal("mytestsite-staging.azurewebsites.net", telemetry.Context.GetInternalContext().NodeName);
        }

        [Fact]
        public void Initialize_WithProductionSlot()
        {
            SetEnvironmentVariables("mytestsite", "Production");

            var initializer = new WebJobsRoleEnvironmentTelemetryInitializer();

            var telemetry = new TraceTelemetry();
            initializer.Initialize(telemetry);

            Assert.Equal("mytestsite", telemetry.Context.Cloud.RoleName);
            Assert.Equal("mytestsite.azurewebsites.net", telemetry.Context.GetInternalContext().NodeName);
        }

        private static void SetEnvironmentVariables(string websiteName, string slotName)
        {
            Environment.SetEnvironmentVariable(WebJobsRoleEnvironmentTelemetryInitializer.AzureWebsiteName, websiteName);
            Environment.SetEnvironmentVariable(WebJobsRoleEnvironmentTelemetryInitializer.AzureWebsiteSlotName, slotName);
        }

        public void Dispose()
        {
            SetEnvironmentVariables(null, null);
        }
    }
}
