// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Loggers
{
    public class WebJobsRoleInstanceProviderTests : IDisposable
    {
        public WebJobsRoleInstanceProviderTests()
        {
            // make sure these are clear before each test
            SetEnvironmentVariables(null, null, null);
        }

        [Fact]
        public void RoleInstanceProvider_UsesWebsiteInstanceId()
        {
            SetEnvironmentVariables("instanceId", "computerName", "containerName");

            var provider = new WebJobsRoleInstanceProvider();
            Assert.Equal("instanceId", provider.GetRoleInstanceName());
        }

        [Fact]
        public void RoleInstanceProvider_UsesComputerName()
        {
            SetEnvironmentVariables(null, "computerName", "containerName");

            var provider = new WebJobsRoleInstanceProvider();
            Assert.Equal("computerName", provider.GetRoleInstanceName());
        }

        [Fact]
        public void RoleInstanceProvider_UsesContainerName()
        {
            SetEnvironmentVariables(null, null, "containerName");

            var provider = new WebJobsRoleInstanceProvider();
            Assert.Equal("containerName", provider.GetRoleInstanceName());
        }

        private static void SetEnvironmentVariables(string instanceId, string computerName, string containerName)
        {
            Environment.SetEnvironmentVariable(WebJobsRoleInstanceProvider.WebSiteInstanceIdKey, instanceId);
            Environment.SetEnvironmentVariable(WebJobsRoleInstanceProvider.ComputerNameKey, computerName);
            Environment.SetEnvironmentVariable(WebJobsRoleInstanceProvider.ContainerNameKey, containerName);
        }

        public void Dispose()
        {
            SetEnvironmentVariables(null, null, null);
        }
    }
}
