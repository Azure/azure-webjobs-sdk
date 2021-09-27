// Copyright (c) .NET Foundation. All rights reserved.
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

            Assert.True(azureStorageProvider.TryGetBlobServiceClientFromConnection(out BlobServiceClient blobServiceClient, ConnectionStringNames.Storage));
            await VerifyBlobServiceClientAvailable(blobServiceClient);
        }

        [Fact]
        public void TestAzureStorageProvider_NoConnectionThrowsException()
        {
            var configuration = new ConfigurationBuilder()
                .Build();

            var azureStorageProvider = GetAzureStorageProvider(configuration);
            Assert.Throws<InvalidOperationException>(() => azureStorageProvider.GetWebJobsBlobContainerClient());

            Assert.False(azureStorageProvider.TryGetBlobServiceClientFromConnection(out BlobServiceClient blobServiceClient, ConnectionStringNames.Storage));
        }

        [Fact]
        public void TestAzureStorageProvider_ConnectionExistsWorksProperly()
        {
            var bytes = Encoding.UTF8.GetBytes("someKey");
            var encodedString = Convert.ToBase64String(bytes);

            // Connection exists in configuration
            var configData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { StorageConnection, $"DefaultEndpointsProtocol=https;AccountName=webHostAccount;AccountKey={encodedString};EndpointSuffix=core.windows.net" },
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var azureStorageProvider = GetAzureStorageProvider(configuration);
            Assert.True(azureStorageProvider.ConnectionExists(ConnectionStringNames.Storage));

            // Connection doesn't exist
            configuration = new ConfigurationBuilder()
                .Build();

            azureStorageProvider = GetAzureStorageProvider(configuration);
            Assert.False(azureStorageProvider.ConnectionExists(ConnectionStringNames.Storage));

            // Connection is a set of configuration settings
            configData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "AzureWebJobsStorAGE:accountName", "testAccount" },
            };
            configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            azureStorageProvider = GetAzureStorageProvider(configuration);
            Assert.True(azureStorageProvider.ConnectionExists(ConnectionStringNames.Storage));

            // Connection is a set of configuration settings
            configData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "AzureWebJobsStorage:blobServiceUri", "https://testAccount.blob.core.windows.net" },
                { "AzureWebJobsStorage:queueServiceUri", "https://testAccount.queue.core.windows.net" }
            };
            configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            azureStorageProvider = GetAzureStorageProvider(configuration);
            Assert.True(azureStorageProvider.ConnectionExists(ConnectionStringNames.Storage));
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

        private static IAzureStorageProvider GetAzureStorageProvider(IConfiguration configuration, JobHostInternalStorageOptions storageOptions = null)
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

            var azureStorageProvider = tempHost.Services.GetRequiredService<IAzureStorageProvider>();
            return azureStorageProvider;
        }
    }
}
