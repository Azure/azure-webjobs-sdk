// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs
{
    internal class Utility
    {
        private const string DefaultConfigurationRootSectionName = "AzureWebJobs";
        private const string ConfigurationRootSectionKey = "AzureWebJobsConfigurationSection";

        public static IConfiguration GetWebJobsConfigurationSection(IConfiguration configuration)
        {
            string configPath = configuration[ConfigurationRootSectionKey] ?? DefaultConfigurationRootSectionName;

            if (string.IsNullOrEmpty(configPath))
            {
                return configuration;
            }

            return configuration.GetSection(configPath);
        }

        public static IConfigurationSection GetExtensionConfigurationSection<T>(IConfiguration configuration) where T : IExtensionConfigProvider
        {
            return GetExtensionConfigurationSection(configuration, GetExtensionConfigurationSectionName<T>());
        }

        public static IConfigurationSection GetExtensionConfigurationSection(IConfiguration configuration, string extensionName)
        {
            string configPath = "extensions";

            if (extensionName != null)
            {
                configPath = ConfigurationPath.Combine(configPath, extensionName);
            }

            var rootConfiguration = GetWebJobsConfigurationSection(configuration);
            return rootConfiguration.GetSection(configPath);
        }

        internal static string GetExtensionAliasFromTypeName(string typeName)
        {
            const string defaultSuffix = "ExtensionConfigProvider";

            if (typeName.EndsWith(defaultSuffix))
            {
                return typeName.Substring(0, typeName.Length - defaultSuffix.Length);
            }

            return typeName;
        }

        private static string GetExtensionConfigurationSectionName<TExtension>() where TExtension : IExtensionConfigProvider
        {
            var type = typeof(TExtension).GetTypeInfo();
            var attribute = type.GetCustomAttribute<ExtensionAttribute>(false);

            return attribute?.ConfigurationSection ?? GetExtensionAliasFromTypeName(type.Name);
        }
    }
}
