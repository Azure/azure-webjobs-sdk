﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Configuration;

namespace Microsoft.Azure.WebJobs.Host
{
    internal static class ConfigurationUtility
    {
        public static string GetSettingFromConfigOrEnvironment(string settingName)
        {
            if (string.IsNullOrEmpty(settingName))
            {
                return null;
            }

            string configValue = ConfigurationManager.AppSettings[settingName];
            if (!string.IsNullOrEmpty(configValue))
            {
                // config values take precedence over environment values
                return configValue;
            }

            return Environment.GetEnvironmentVariable(settingName) ?? configValue;
        }

        public static string GetConnectionFromConfigOrEnvironment(string connectionName)
        {
            string configValue = null;
            var connectionStringEntry = ConfigurationManager.ConnectionStrings[connectionName];
            if (connectionStringEntry != null)
            {
                configValue = connectionStringEntry.ConnectionString;
            }

            if (!string.IsNullOrEmpty(configValue))
            {
                // config values take precedence over environment values
                return configValue;
            }

            return Environment.GetEnvironmentVariable(connectionName) ?? configValue;
        }

        public static bool IsSettingEnabled(string settingName)
        {
            // check the target setting and return false (disabled) if the value exists
            // and is "falsey"
            string value = GetSettingFromConfigOrEnvironment(settingName);
            if (!string.IsNullOrEmpty(value) &&
                (string.Compare(value, "1", StringComparison.OrdinalIgnoreCase) == 0 ||
                 string.Compare(value, "true", StringComparison.OrdinalIgnoreCase) == 0))
            {
                return true;
            }

            return false;
        }
    }
}
