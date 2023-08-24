// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host
{
    public static class AppServicesHostingUtility
    {
        private const int BytesPerGB = 1024 * 1024 * 1024;

        public static long GetMemoryLimitBytes(string sku = null, int? numCores = null)
        {
            sku ??= Utility.GetWebsiteSku();
            numCores ??= Utility.GetEffectiveCoresCount();

            if (!string.IsNullOrEmpty(sku))
            {
                float memoryGBPerCore = GetMemoryGBPerCore(sku);

                if (memoryGBPerCore > 0)
                {
                    double memoryLimitBytes = memoryGBPerCore * numCores.Value * BytesPerGB;

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
