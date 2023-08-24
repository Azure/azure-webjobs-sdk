// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Host.Config
{
    internal class ConcurrencyOptionsSetup : IConfigureOptions<ConcurrencyOptions>
    {

        public void Configure(ConcurrencyOptions options)
        {
            // TODO: Once Memory monitoring is public add this back.
            // For now, the memory throttle is internal only for testing.
            // https://github.com/Azure/azure-webjobs-sdk/issues/2733
            //ConfigureMemoryOptions(options);
        }

        internal static void ConfigureMemoryOptions(ConcurrencyOptions options)
        {
            ConfigureMemoryOptions(options);
        }

        internal static void ConfigureMemoryOptions(ConcurrencyOptions options, string sku = null, int? numCores = null)
        {
            long memoryLimitBytes = AppServicesHostingUtility.GetMemoryLimitBytes(sku, numCores);
            if (memoryLimitBytes > 0)
            {
                // if we're able to determine the memory limit, apply it
                options.TotalAvailableMemoryBytes = memoryLimitBytes;
            }
        }
    }
}
