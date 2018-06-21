﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    static class TestExtensions
    {
        public static XStorageAccount GetStorageAccount(this IHost host)
        {
            var provider = host.Services.GetRequiredService<StorageAccountProvider>(); // $$$ ok?
            return provider.GetHost();
        }
    }
}
