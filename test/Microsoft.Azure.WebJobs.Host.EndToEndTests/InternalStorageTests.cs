// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    public class InternalStorageTests
    {
        // End-2-end test that we can run a JobHost from purely a SAS connection string. 
        [Fact]
        public async Task Test()
        {
            var containerName = "test-internal1";

            var acs = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            if (acs == null)
            {
                Assert.Fail("Missing AzureWebJobsStorage setting");
            }

            // Create a real Blob Container Sas URI
            var blobServiceClient = new BlobServiceClient(acs);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(); // this will throw if acs is bad;
            var fakeSasUri = containerClient.GenerateSasUri(BlobContainerSasPermissions.Read | BlobContainerSasPermissions.Write | BlobContainerSasPermissions.List, DateTime.UtcNow.AddDays(10));

            var prog = new BasicProg();

            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost(prog, b =>
                {
                    RuntimeStorageWebJobsBuilderExtensions.AddAzureStorageCoreServices(b);
                })
                .ConfigureAppConfiguration(config =>
                {
                    // Set env to the SAS container and clear out all other storage. 
                    config.AddInMemoryCollection(new Dictionary<string, string>()
                    {
                            { "AzureWebJobs:InternalSasBlobContainer", fakeSasUri.ToString() },
                            { "AzureWebJobsStorage", null },
                            { "AzureWebJobsDashboard", null }
                    });
                })
                .Build();

            var internalOptions = host.Services.GetService<IOptions<JobHostInternalStorageOptions>>();
            Assert.NotNull(internalOptions);
            Assert.Equal(fakeSasUri.ToString(), internalOptions.Value.InternalSasBlobContainer);

            Assert.True(host.Services.GetService<IAzureBlobStorageProvider>().TryCreateHostingBlobContainerClient(out BlobContainerClient actualContainer));
            Assert.Equal(containerClient.Name, actualContainer.Name);

            await host.GetJobHost().CallAsync(nameof(BasicProg.Foo));

            Assert.Equal(1, prog._count); // Verify successfully called.
        }

        public class BasicProg
        {
            public int _count;

            [Singleton] // Singleton will force usage of SAS container
            [NoAutomaticTrigger]
            public void Foo()
            {
                _count++;
            }
        }
    }
}