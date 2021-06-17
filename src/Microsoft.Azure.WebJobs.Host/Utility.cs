// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs
{
    internal static class Utility
    {
        public static string GetInstanceId()
        {
            string instanceId = Environment.GetEnvironmentVariable(Constants.AzureWebsiteInstanceId)
                     ?? GetStableHash(Environment.MachineName).ToString("X").PadLeft(32, '0');

            return instanceId.Substring(0, Math.Min(instanceId.Length, 32));
        }

        /// <summary>
        /// Computes a stable non-cryptographic hash
        /// </summary>
        /// <param name="value">The string to use for computation</param>
        /// <returns>A stable, non-cryptographic, hash</returns>
        internal static int GetStableHash(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            unchecked
            {
                int hash = 23;
                foreach (char c in value)
                {
                    hash = (hash * 31) + c;
                }
                return hash;
            }
        }

        public static string FlattenException(Exception ex, Func<string, string> sourceFormatter = null, bool includeSource = true)
        {
            StringBuilder flattenedErrorsBuilder = new StringBuilder();
            string lastError = null;
            sourceFormatter = sourceFormatter ?? ((s) => s);

            if (ex is AggregateException)
            {
                ex = ex.InnerException;
            }

            do
            {
                StringBuilder currentErrorBuilder = new StringBuilder();
                if (includeSource && !string.IsNullOrEmpty(ex.Source))
                {
                    currentErrorBuilder.AppendFormat("{0}: ", sourceFormatter(ex.Source));
                }

                currentErrorBuilder.Append(ex.Message);

                if (!ex.Message.EndsWith("."))
                {
                    currentErrorBuilder.Append(".");
                }

                // sometimes inner exceptions are exactly the same
                // so first check before duplicating
                string currentError = currentErrorBuilder.ToString();
                if (lastError == null ||
                    string.Compare(lastError.Trim(), currentError.Trim()) != 0)
                {
                    if (flattenedErrorsBuilder.Length > 0)
                    {
                        flattenedErrorsBuilder.Append(" ");
                    }
                    flattenedErrorsBuilder.Append(currentError);
                }

                lastError = currentError;
            }
            while ((ex = ex.InnerException) != null);

            return flattenedErrorsBuilder.ToString();
        }

        public static int GetEffectiveCoresCount()
        {
            // When not running on VMSS, the dynamic plan has some limits that mean that a given instance is using effectively a single core,
            // so we should not use Environment.Processor count in this case.
            var effectiveCores = (IsConsumptionSku() && !IsVMSS()) ? 1 : Environment.ProcessorCount;
            return effectiveCores;
        }

        public static string GetWebsiteSku()
        {
            return Environment.GetEnvironmentVariable(Constants.AzureWebsiteSku);
        }

        public static bool IsConsumptionSku()
        {
            string value = GetWebsiteSku();
            return string.Equals(value, Constants.DynamicSku, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsVMSS()
        {
            string value = Environment.GetEnvironmentVariable("RoleInstanceId");
            return value != null && value.IndexOf("HostRole", StringComparison.OrdinalIgnoreCase) >= 0;
        }


        public static IConfigurationSection GetExtensionConfigurationSection<T>(IConfiguration configuration) where T : IExtensionConfigProvider
        {
            return configuration.GetWebJobsExtensionConfigurationSection(GetExtensionConfigurationSectionName<T>());
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

        internal static string GetFunctionName(MethodInfo methodInfo)
        {
            FunctionNameAttribute functionNameAttribute = TypeUtility.GetHierarchicalAttributeOrNull<FunctionNameAttribute>(methodInfo);
            return (functionNameAttribute != null) ? functionNameAttribute.Name : methodInfo.Name;
        }

        internal static string GetFunctionShortName(MethodInfo methodInfo)
        {
            FunctionNameAttribute functionNameAttribute = TypeUtility.GetHierarchicalAttributeOrNull<FunctionNameAttribute>(methodInfo);
            return (functionNameAttribute != null) ? functionNameAttribute.Name : methodInfo.GetShortName();
        }

        public static IEnumerable<TElement> TakeLastN<TElement>(this IEnumerable<TElement> source, int take)
        {
            if (take < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(take));
            }

            int skipCount = Math.Max(0, source.Count() - take);

            return source.Skip(skipCount).Take(take);
        }

        private static string GetExtensionConfigurationSectionName<TExtension>() where TExtension : IExtensionConfigProvider
        {
            var type = typeof(TExtension).GetTypeInfo();
            var attribute = type.GetCustomAttribute<ExtensionAttribute>(false);

            return attribute?.ConfigurationSection ?? GetExtensionAliasFromTypeName(type.Name);
        }
    }
}
