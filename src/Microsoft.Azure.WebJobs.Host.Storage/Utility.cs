// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.Azure.WebJobs.Extensions.Storage
{
    static class Utility
    {
        internal static int GetProcessorCount()
        {
            int processorCount = 1;
            var skuValue = Environment.GetEnvironmentVariable(Constants.AzureWebsiteSku);
            if (!string.Equals(skuValue, Constants.DynamicSku, StringComparison.OrdinalIgnoreCase))
            {
                processorCount = Environment.ProcessorCount;
            }
            return processorCount;
        }
    }
}