// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs
{
    internal class Utility
    {
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

        private static string GetExtensionConfigurationSectionName<TExtension>() where TExtension : IExtensionConfigProvider
        {
            var type = typeof(TExtension).GetTypeInfo();
            var attribute = type.GetCustomAttribute<ExtensionAttribute>(false);

            return attribute?.ConfigurationSection ?? GetExtensionAliasFromTypeName(type.Name);
        }

        internal static async Task<bool> WaitForNextExecutionAttempt(IFunctionInstance functionInstance, IDelayedException functionResult, IRetryStrategy retryStrategy, ILogger logger, int attempt)
        {
            if (functionResult == null)
            {
                // function invocation succeeded 
                return true;
            }
            if (retryStrategy.MaxRetryCount != -1 && ++attempt >= retryStrategy.MaxRetryCount)
            {
                // no.of retries exceeded
                return true;
            }

            // Build retry context
            var retryContext = new RetryContext
            {
                RetryCount = attempt,
                Exception = functionResult.Exception,
                Instance = functionInstance
            };

            TimeSpan nextDelay = retryStrategy.GetNextDelay(retryContext);
            logger.LogFunctionRetryAttempt(nextDelay, attempt, retryStrategy.MaxRetryCount);
            await Task.Delay(nextDelay);
            return false;
        }
    }
}
