﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests
{
    public class AzureStorageProviderTests
    {
        private const string StorageConnection = "AzureWebJobsStorage";

        [Fact]
        public async Task TestAzureStorageProvider_ConnectionInWebHostConfiguration()
        {
            var testConfiguration = new ConfigurationBuilder()
                    .AddEnvironmentVariables()
                    .AddTestSettings()
                    .Build();
            var testData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "ConnectionStrings:AzureWebJobsStorage", testConfiguration.GetWebJobsConnectionString(StorageConnection) },
                { "AzureWebJobsStorage", "" }
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(testData)
                .Build();

            var azureStorageProvider = GetAzureStorageProvider(configuration);
            var container = azureStorageProvider.GetWebJobsBlobContainerClient();
            await VerifyContainerClientAvailable(container);

            Assert.True(azureStorageProvider.TryGetBlobServiceClientFromConnection(ConnectionStringNames.Storage, out BlobServiceClient blobServiceClient));
            await VerifyBlobServiceClientAvailable(blobServiceClient);
        }

        [Fact]
        public void TestAzureStorageProvider_NoConnectionThrowsException()
        {
            var configuration = new ConfigurationBuilder()
                .Build();

            var azureStorageProvider = GetAzureStorageProvider(configuration);
            Assert.Throws<InvalidOperationException>(() => azureStorageProvider.GetWebJobsBlobContainerClient());

            Assert.False(azureStorageProvider.TryGetBlobServiceClientFromConnection(ConnectionStringNames.Storage, out BlobServiceClient blobServiceClient));
        }

        private async Task VerifyBlobServiceClientAvailable(BlobServiceClient client)
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

        private async Task VerifyContainerClientAvailable(BlobContainerClient client)
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

        private static IAzureBlobStorageProvider GetAzureStorageProvider(IConfiguration configuration, JobHostInternalStorageOptions storageOptions = null)
        {
            IHost tempHost = new HostBuilder()
                .ConfigureServices(services =>
                {
                    // Override configuration
                    services.AddSingleton(configuration);
                    services.AddAzureStorageCoreServices();

                    if (storageOptions != null)
                    {
                        services.AddTransient<IOptions<JobHostInternalStorageOptions>>(s => new OptionsWrapper<JobHostInternalStorageOptions>(storageOptions));
                    }
                }).Build();

            var azureStorageProvider = tempHost.Services.GetRequiredService<IAzureBlobStorageProvider>();
            return azureStorageProvider;
        }
    }
}
