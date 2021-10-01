// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests
{
    /// <summary>
    /// Tests whether the StorageClientProvider can properly create a client and send a request
    /// </summary>
    public class BlobServiceClientProviderTests
    {
        private const string StorageConnection = "AzureWebJobsStorage";

        private readonly BlobServiceClientProvider _blobServiceClientProvider;
        private readonly IConfiguration _configuration;

        public BlobServiceClientProviderTests()
        {
            _configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddTestSettings()
                .Build();

            _blobServiceClientProvider = GetBlobServiceClientProvider(_configuration);
        }

        [Fact]
        public async Task TestBlobStorageProvider_TryConnectionName()
        {
            Assert.True(_blobServiceClientProvider.TryCreate(StorageConnection, _configuration, out BlobServiceClient client));
            await VerifyServiceAvailable(client);
        }

        [Fact]
        public async Task TestBlobStorageProvider_ConnectionName()
        {
            BlobServiceClient client = _blobServiceClientProvider.Create(StorageConnection, _configuration);
            await VerifyServiceAvailable(client);
        }

        [Fact]
        public async Task TestBlobStorageProvider_ConnectionNameWithResolver()
        {
            var resolver = new DefaultNameResolver(_configuration);

            BlobServiceClient client = _blobServiceClientProvider.Create(StorageConnection, resolver, _configuration);
            await VerifyServiceAvailable(client);
        }

        [Fact]
        public async Task TestBlobStorageProvider_TryConnectionStringVariants()
        {
            var testData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "ConnectionStrings:AzureWebJobsStorage", Environment.GetEnvironmentVariable(StorageConnection) },
                { "AzureWebJobsStorage", "" }
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(testData)
                .AddTestSettings()
                .Build();

            var blobServiceClientProvider = GetBlobServiceClientProvider(configuration);
            BlobServiceClient client = blobServiceClientProvider.Create(StorageConnection, configuration);

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

        private static BlobServiceClientProvider GetBlobServiceClientProvider(IConfiguration configuration, JobHostInternalStorageOptions storageOptions = null)
        {
            IHost tempHost = new HostBuilder()
                .ConfigureServices(services =>
                {
                    // Override configuration
                    services.AddSingleton(configuration);
                    services.AddAzureStorageCoreServices();
                }).Build();

            var blobServiceClientProvider = tempHost.Services.GetRequiredService<BlobServiceClientProvider>();
            return blobServiceClientProvider;
        }
    }
}
