// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    static class TestExtensions
    {
        public static StorageAccount GetStorageAccount(this IHost host)
        {
            var provider = host.Services.GetService<StorageAccountProvider>();
            return provider?.GetHost();
        }
    }
}
