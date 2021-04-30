// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.StorageProvider.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Microsoft.Azure.WebJobs.StorageProvider.FunctionalTests
{
    public class BlobStorageProviderTests
    {
        private const string StorageConnection = "AzureWebJobsStorage";

        private readonly BlobServiceClientProvider _blobServiceClientProvider;
        private readonly IConfiguration _configuration; 

        public BlobStorageProviderTests()
        {
            IHost host = new HostBuilder()
                    .ConfigureDefaultTestHost(b =>
                    {
                        b.Services.AddAzureStorageBlobs();
                    })
                    .Build();

            _blobServiceClientProvider = host.Services.GetServices<BlobServiceClientProvider>().OfType<BlobServiceClientProvider>().Single();
            _configuration = host.Services.GetServices<IConfiguration>().OfType<IConfiguration>().Single();
        }

        [Fact]
        public async Task TestBlobStorageProvider_TryConnectionName()
        {
            Assert.True(_blobServiceClientProvider.TryGet(StorageConnection, out BlobServiceClient client));
            await VerifyServiceAvailable(client);
        }

        [Fact]
        public async Task TestBlobStorageProvider_ConnectionName()
        {
            BlobServiceClient client = _blobServiceClientProvider.Get(StorageConnection);
            await VerifyServiceAvailable(client);
        }

        [Fact]
        public async Task TestBlobStorageProvider_ConnectionNameWithResolver()
        {
            var resolver = new DefaultNameResolver(_configuration);

            BlobServiceClient client = _blobServiceClientProvider.Get(StorageConnection, resolver);
            await VerifyServiceAvailable(client);
        }

        [Fact]
        public async Task TestBlobStorageProvider_TryConnectionString()
        {
            var connectionString = _configuration[StorageConnection];

            Assert.True(_blobServiceClientProvider.TryGetFromConnectionString(connectionString, out BlobServiceClient client));
            await VerifyServiceAvailable(client);
        }

        private async Task VerifyServiceAvailable(BlobServiceClient client)
        {
            try
            {
                var propertiesResponse = await client.GetPropertiesAsync();
                Assert.True(true);
            }
            catch (Exception e)
            {
                Assert.False(true, $"Could not establish connection to BlobService. {e}");
            }
        }
    }
}