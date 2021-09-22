// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Azure.Storage.Queues;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    static class TestExtensions
    {
        public static QueueServiceClient GetQueueServiceClient(this IHost host)
        {
            var configuration = host.Services.GetRequiredService<IConfiguration>();
            var queueServiceClientProvider = host.Services.GetRequiredService<QueueServiceClientProvider>();
            if (queueServiceClientProvider.TryGet(ConnectionStringNames.Storage, configuration, out QueueServiceClient queueServiceClient))
            {
                return queueServiceClient;
            }
            else
            {
                return null;
            }
        }
    }
}
