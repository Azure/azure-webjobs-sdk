// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Loggers
{
    public class WebJobsRoleEnvironmentTelemetryInitializerTests
    {
        public WebJobsRoleEnvironmentTelemetryInitializerTests() { }

        [Fact]
        public void Initialize_DoesNotThrow_WhenNoEnvironmentVariables()
        {
            using (EnvVarHolder.Set(WebJobsRoleEnvironmentTelemetryInitializer.AzureWebsiteName, null))
            using (EnvVarHolder.Set(WebJobsRoleEnvironmentTelemetryInitializer.AzureWebsiteSlotName, null))
            using (EnvVarHolder.Set(WebJobsRoleEnvironmentTelemetryInitializer.AzureWebsiteCloudRoleName, null))
            {
                var initializer = new WebJobsRoleEnvironmentTelemetryInitializer();

                var telemetry = new TraceTelemetry();
                initializer.Initialize(telemetry);

                Assert.Null(telemetry.Context.Cloud.RoleName);
                Assert.Null(telemetry.Context.GetInternalContext().NodeName);
            }
        }

        [Fact]
        public void Initialize_WithSlot()
        {
            using (EnvVarHolder.Set(WebJobsRoleEnvironmentTelemetryInitializer.AzureWebsiteName, "mytestsite"))
            using (EnvVarHolder.Set(WebJobsRoleEnvironmentTelemetryInitializer.AzureWebsiteSlotName, "Staging"))
            using (EnvVarHolder.Set(WebJobsRoleEnvironmentTelemetryInitializer.AzureWebsiteCloudRoleName, null))
            {
                var initializer = new WebJobsRoleEnvironmentTelemetryInitializer();

                var telemetry = new TraceTelemetry();
                initializer.Initialize(telemetry);

                Assert.Equal("mytestsite-staging", telemetry.Context.Cloud.RoleName);
                Assert.Equal("mytestsite-staging.azurewebsites.net", telemetry.Context.GetInternalContext().NodeName);
            }
        }

        [Fact]
        public void Initialize_WithProductionSlot()
        {
            using (EnvVarHolder.Set(WebJobsRoleEnvironmentTelemetryInitializer.AzureWebsiteName, "mytestsite"))
            using (EnvVarHolder.Set(WebJobsRoleEnvironmentTelemetryInitializer.AzureWebsiteSlotName, "Production"))
            using (EnvVarHolder.Set(WebJobsRoleEnvironmentTelemetryInitializer.AzureWebsiteCloudRoleName, null))
            {
                var initializer = new WebJobsRoleEnvironmentTelemetryInitializer();

                var telemetry = new TraceTelemetry();
                initializer.Initialize(telemetry);

                Assert.Equal("mytestsite", telemetry.Context.Cloud.RoleName);
                Assert.Equal("mytestsite.azurewebsites.net", telemetry.Context.GetInternalContext().NodeName);
            }
        }

        [Fact]
        public void Initialize_WithWebsiteCloudRoleName()
        {
            var testCloudRoleName = "mycloudrolename";

            using (EnvVarHolder.Set(WebJobsRoleEnvironmentTelemetryInitializer.AzureWebsiteName, "mytestsite"))
            using (EnvVarHolder.Set(WebJobsRoleEnvironmentTelemetryInitializer.AzureWebsiteSlotName, "Production"))
            using (EnvVarHolder.Set(WebJobsRoleEnvironmentTelemetryInitializer.AzureWebsiteCloudRoleName, testCloudRoleName))
            {
                var initializer = new WebJobsRoleEnvironmentTelemetryInitializer();

                var telemetry = new TraceTelemetry();
                initializer.Initialize(telemetry);

                Assert.Equal(testCloudRoleName, telemetry.Context.Cloud.RoleName);
            }
        }
    }
}
