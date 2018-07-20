// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
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
                Assert.False(true, "Missing AzureWebJobsStorage setting");
            }

            // Create a real Blob Container Sas URI
            var account1 = CloudStorageAccount.Parse(acs);
            var client = account1.CreateCloudBlobClient();
            var container = client.GetContainerReference(containerName);
            await container.CreateIfNotExistsAsync(); // this will throw if acs is bad

            var now = DateTime.UtcNow;
            var sig = container.GetSharedAccessSignature(new SharedAccessBlobPolicy
            {
                Permissions = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.Write | SharedAccessBlobPermissions.List,
                SharedAccessStartTime = now.AddDays(-10),
                SharedAccessExpiryTime = now.AddDays(10)
            });

            var fakeSasUri = container.Uri + sig;
            var prog = new BasicProg();

            IHost host = RuntimeConfigurationExtensions.AddAzureStorageCoreServices(new HostBuilder()
                .ConfigureDefaultTestHost(prog)
                .AddAzureStorage()
                .ConfigureAppConfiguration(config =>
                {
                    // Set env to the SAS container and clear out all other storage. 
                    config.AddInMemoryCollection(new Dictionary<string, string>()
                    {
                            { "AzureWebJobs:InternalSasBlobContainer", fakeSasUri },
                            { "AzureWebJobsStorage", null },
                            { "AzureWebJobsDashboard", null }
                    });
                })
)
                .Build();

            var internalOptions = host.Services.GetService<DistributedLockManagerContainerProvider>();
            Assert.NotNull(internalOptions);
            Assert.Equal(container.Name, internalOptions.InternalContainer.Name);

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