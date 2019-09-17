// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    internal interface IRoleInstanceProvider
    {
        string GetRoleInstanceName();
    }

    internal class WebJobsRoleInstanceProvider : IRoleInstanceProvider
    {
        internal const string ComputerNameKey = "COMPUTERNAME";
        internal const string WebSiteInstanceIdKey = "WEBSITE_INSTANCE_ID";
        internal const string ContainerNameKey = "CONTAINER_NAME";

        private readonly string _roleInstanceName = GetRoleInstance();

        public string GetRoleInstanceName()
        {
            return _roleInstanceName;
        }

        private static string GetRoleInstance()
        {
            string instanceName = Environment.GetEnvironmentVariable(WebSiteInstanceIdKey);
            if (string.IsNullOrEmpty(instanceName))
            {
                instanceName = Environment.GetEnvironmentVariable(ComputerNameKey);
                if (string.IsNullOrEmpty(instanceName))
                {
                    instanceName = Environment.GetEnvironmentVariable(ContainerNameKey);
                }
            }

            return instanceName;
        }
    }
}
