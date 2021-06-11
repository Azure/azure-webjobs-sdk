// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Host.Config
{
    internal class ConcurrencyOptionsSetup : IConfigureOptions<ConcurrencyOptions>
    {
        private const int BytesPerGB = 1024 * 1024 * 1024;

        public void Configure(ConcurrencyOptions options)
        {
            // TODO: Once Memory monitoring is public add this back.
            // For now, the memory throttle is internal only for testing.
            // https://github.com/Azure/azure-webjobs-sdk/issues/2733
            //ConfigureMemoryOptions(options);
        }

        internal static void ConfigureMemoryOptions(ConcurrencyOptions options)
        {
            string sku = Utility.GetWebsiteSku();
            int numCores = Utility.GetEffectiveCoresCount();
            ConfigureMemoryOptions(options, sku, numCores);
        }

        internal static void ConfigureMemoryOptions(ConcurrencyOptions options, string sku, int numCores)
        {
            long memoryLimitBytes = GetMemoryLimitBytes(sku, numCores);
            if (memoryLimitBytes > 0)
            {
                // if we're able to determine the memory limit, apply it
                options.TotalAvailableMemoryBytes = memoryLimitBytes;
            }
        }

        internal static long GetMemoryLimitBytes(string sku, int numCores)
        {
            if (!string.IsNullOrEmpty(sku))
            {
                float memoryGBPerCore = GetMemoryGBPerCore(sku);

                if (memoryGBPerCore > 0)
                {
                    double memoryLimitBytes = memoryGBPerCore * numCores * BytesPerGB;

                    if (string.Equals(sku, "IsolatedV2", StringComparison.OrdinalIgnoreCase) && numCores == 8)
                    {
                        // special case for upper tier IsolatedV2 where GB per Core
                        // isn't cleanly linear
                        memoryLimitBytes = (float)23 * BytesPerGB;
                    }

                    return (long)memoryLimitBytes;
                }
            }

            // unable to determine memory limit
            return -1;
        }

        internal static float GetMemoryGBPerCore(string sku)
        {
            if (string.IsNullOrEmpty(sku))
            {
                return -1;
            }

            // These memory allowances are based on published limits:
            // Dynamic SKU: https://docs.microsoft.com/en-us/azure/azure-functions/functions-scale#service-limits
            // Premium SKU: https://docs.microsoft.com/en-us/azure/azure-functions/functions-premium-plan?tabs=portal#available-instance-skus
            // Dedicated SKUs: https://azure.microsoft.com/en-us/pricing/details/app-service/windows/
            switch (sku.ToLower())
            {
                case "free":
                case "shared":
                    return 1;
                case "dynamic":
                    return 1.5F;
                case "basic":
                case "standard":
                    return 1.75F;
                case "premiumv2":
                case "isolated":
                case "elasticpremium":
                    return 3.5F;
                case "premiumv3":
                case "isolatedv2":
                    return 4;
                default:
                    return -1;
            }
        }
    }
}
