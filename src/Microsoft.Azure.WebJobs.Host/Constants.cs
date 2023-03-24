// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host
{
    internal static class Constants
    {
        public const string WebJobsConfigurationSectionName = "AzureWebJobs";
        public const string EnvironmentSettingName = "AzureWebJobsEnv";
        public const string DevelopmentEnvironmentValue = "Development";
        public const string DynamicSku = "Dynamic";
        public const string ElasticPremiumSku = "ElasticPremium";
        public const string AzureWebsiteSku = "WEBSITE_SKU";
        public const string AzureWebJobsShutdownFile = "WEBJOBS_SHUTDOWN_FILE";
        public const string AzureWebsiteInstanceId = "WEBSITE_INSTANCE_ID";
        public const string AzureWebsiteContainerName = "CONTAINER_NAME";
        public const string DateTimeFormatString = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffK";
    }
}
