// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Extensions.Hosting
{
    public static class RuntimeStorageWebJobsBuilderExtensions
    {
        // WebJobs v1 Classic logging. Needed for dashboard.         
        // $$$ Update title? 
        public static IWebJobsBuilder AddDashboardLogging(this IWebJobsBuilder builder)
        {
            builder.Services.AddDashboardLogging();

            return builder;
        }

        // Make the Runtime itself use storage for its internal operations. 
        // Uses v1 app settings, via a LegacyConfigSetup object. 
        public static IWebJobsBuilder AddAzureStorageCoreServices(this IWebJobsBuilder builder)
        {
            // Replace existing runtime services with storage-backed implementations.
            // Add runtime services that depend on storage.
            builder.Services.AddSingleton<IDistributedLockManager>(provider => Create(provider));

            // Used specifically for the CloudBlobContainerDistributedLockManager implementaiton 
            builder.Services.TryAddSingleton<DistributedLockManagerContainerProvider>();

            builder.Services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<StorageAccountOptions>, StorageAccountOptionsSetup>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<JobHostInternalStorageOptions>, CoreWebJobsOptionsSetup<JobHostInternalStorageOptions>>());

            return builder;
        }

        // This is only called if the host didn't already provide an implementation 
        private static IDistributedLockManager Create(IServiceProvider provider)
        {
            // $$$ get rid of LegacyConfig
            var opts = provider.GetRequiredService<IOptions<StorageAccountOptions>>();

            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            
            var sas = provider.GetService<DistributedLockManagerContainerProvider>();

            CloudBlobContainer container;

            if (sas != null && sas.InternalContainer != null)
            {
                container = sas.InternalContainer;
            }
            else
            {
                var config = opts.Value;
                CloudStorageAccount account = config.GetStorageAccount();
                if (account == null)
                {
                    return new InMemorySingletonManager();
                }

                var blobClient = account.CreateCloudBlobClient();
                container = blobClient.GetContainerReference(HostContainerNames.Hosts);
            }

            var lockManager = new CloudBlobContainerDistributedLockManager(container, loggerFactory);            
            return lockManager;
        }
    }
}
