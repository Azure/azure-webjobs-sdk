// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Host
{
    internal static class ConfigurationUtility
    {
        private static Func<IConfiguration> _configurationFactory = BuildConfiguration;
        private static Lazy<IConfiguration> _configuration = new Lazy<IConfiguration>(BuildConfiguration);

        private static IConfiguration Configuration => _configuration.Value;

        public static string GetSetting(string settingName)
        {
            if (string.IsNullOrEmpty(settingName))
            {
                return null;
            }

            return Configuration[settingName];
        }

        // The fallback to reading the connection string from the configuration/app setting
        // is here to maintain legacy behavior.
        public static string GetConnectionString(string connectionName)
            => Configuration.GetConnectionString(connectionName) ?? Configuration[connectionName];

        public static void SetConfigurationFactory(Func<IConfiguration> configurationRootFactory)
        {
            _configurationFactory = configurationRootFactory;
            Reset();
        }

        public static bool IsSettingEnabled(string settingName)
        {
            // check the target setting and return false (disabled) if the value exists
            // and is "falsey"
            string value = GetSetting(settingName);
            if (!string.IsNullOrEmpty(value) &&
                (string.Compare(value, "1", StringComparison.OrdinalIgnoreCase) == 0 ||
                 string.Compare(value, "true", StringComparison.OrdinalIgnoreCase) == 0))
            {
                return true;
            }

            return false;
        }

        private static IConfigurationRoot BuildConfiguration()
        {
            var configurationBuilder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables();

            return configurationBuilder.Build();
        }

        internal static void Reset()
        {
            _configuration = new Lazy<IConfiguration>(_configurationFactory);
        }
    }
}
