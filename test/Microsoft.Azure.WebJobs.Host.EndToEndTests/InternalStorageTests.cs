// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json.Linq;
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
            
            // Set env to the SAS container and clear out all other storage. 
            using (EnvVarHolder.Set("AzureWebJobsInternalSasBlobContainer", fakeSasUri))
            using (EnvVarHolder.Set("AzureWebJobsStorage", null))
            using (EnvVarHolder.Set("AzureWebJobsDashboard", null))
            {
                var prog = new BasicProg();
                var activator = new FakeActivator(prog);
                JobHostConfiguration config = new JobHostConfiguration()
                {
                    TypeLocator = new FakeTypeLocator(typeof(BasicProg))                    
                };

                Assert.NotNull(config.InternalStorageConfiguration);
                Assert.Equal(container.Name, config.InternalStorageConfiguration.InternalContainer.Name);

                config.JobActivator = activator;
                config.HostId = Guid.NewGuid().ToString("n");
                config.DashboardConnectionString = null;
                config.StorageConnectionString = null;


                var host = new JobHost(config);
                await host.CallAsync("Foo");

                Assert.Equal(1, prog._count); // Verify successfully called.
            }
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