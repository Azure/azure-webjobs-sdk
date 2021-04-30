// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.StorageProvider.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Microsoft.Azure.WebJobs.StorageProvider.FunctionalTests
{
    public class QueueStorageProviderTests
    {
        private const string StorageConnection = "AzureWebJobsStorage";

        private readonly QueueServiceClientProvider _queueServiceClientProvider;
        private readonly IConfiguration _configuration;

        public QueueStorageProviderTests()
        {
            IHost host = new HostBuilder()
                    .ConfigureDefaultTestHost(b =>
                    {
                        b.Services.AddAzureStorageQueues();
                    })
                    .Build();

            _queueServiceClientProvider = host.Services.GetServices<QueueServiceClientProvider>().OfType<QueueServiceClientProvider>().Single();
            _configuration = host.Services.GetServices<IConfiguration>().OfType<IConfiguration>().Single();
        }

        [Fact]
        public async Task TestQueueStorageProvider_TryConnectionName()
        {
            Assert.True(_queueServiceClientProvider.TryGet(StorageConnection, out QueueServiceClient client));
            await VerifyServiceAvailable(client);
        }

        [Fact]
        public async Task TestQueueStorageProvider_ConnectionName()
        {
            QueueServiceClient client = _queueServiceClientProvider.Get(StorageConnection);
            await VerifyServiceAvailable(client);
        }

        [Fact]
        public async Task TestQueueStorageProvider_ConnectionNameWithResolver()
        {
            var resolver = new DefaultNameResolver(_configuration);

            QueueServiceClient client = _queueServiceClientProvider.Get(StorageConnection, resolver);
            await VerifyServiceAvailable(client);
        }

        [Fact]
        public async Task TestQueueStorageProvider_TryConnectionString()
        {
            var connectionString = _configuration[StorageConnection];

            Assert.True(_queueServiceClientProvider.TryGetFromConnectionString(connectionString, out QueueServiceClient client));
            await VerifyServiceAvailable(client);
        }

        private async Task VerifyServiceAvailable(QueueServiceClient client)
        {
            try
            {
                var propertiesResponse = await client.GetPropertiesAsync();
                Assert.True(true);
            }
            catch (Exception e)
            {
                Assert.False(true, $"Could not establish connection to QueueService. {e}");
            }
        }
    }
}