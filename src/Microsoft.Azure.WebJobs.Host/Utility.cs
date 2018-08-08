// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
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

        public static IConfigurationSection GetExtensionConfigurationSection<T>(IConfiguration configuration)
            where T : IExtensionConfigProvider
        {
            return GetExtensionConfigurationSection(configuration, GetExtensionName<T>());
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

        public static string GetExtensionName<TExtension>()
        {
            return GetExtensionName(typeof(TExtension));
        }

        public static string GetExtensionName(IExtensionConfigProvider extension)
        {
            if (extension == null)
            {
                throw new ArgumentNullException(nameof(extension));
            }

            return GetExtensionName(extension.GetType());
        }

        private static string GetExtensionName(Type extensionType)
        {
            ExtensionAttribute attribute = extensionType.GetTypeInfo().GetCustomAttribute<ExtensionAttribute>(false);

            if (attribute != null)
            {
                return attribute.Name;
            }

            const string defaultSuffix = "ExtensionConfigProvider";

            string typeName = extensionType.Name;

            if (typeName.EndsWith(defaultSuffix))
            {
                return typeName.Substring(0, typeName.Length - defaultSuffix.Length);
            }
            return typeName;
        }
    }
}
