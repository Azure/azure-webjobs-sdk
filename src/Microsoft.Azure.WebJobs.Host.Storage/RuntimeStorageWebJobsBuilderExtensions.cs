// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.Hosting
{
    public static class RuntimeStorageWebJobsBuilderExtensions
    {
        // WebJobs v1 Classic logging. Needed for dashboard.         
        [Obsolete("Dashboard is being deprecated. Use AppInsights.")]
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

            builder.AddAzureStorage();

            builder.Services.TryAddSingleton<IDelegatingHandlerProvider, DefaultDelegatingHandlerProvider>();

            return builder;
        }

        // This is only called if the host didn't already provide an implementation 
        private static IDistributedLockManager Create(IServiceProvider provider)
        {
            // $$$ get rid of LegacyConfig
            var opts = provider.GetRequiredService<IOptions<HostStorageProvider>>();

            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();

            var sas = provider.GetService<DistributedLockManagerContainerProvider>();

            BlobContainerClient containerClient;

            if (sas != null && sas.InternalContainerClient != null)
            {
                containerClient = sas.InternalContainerClient;
            }
            else
            {
                try
                {
                    var hostStorageProvider = opts.Value;
                    BlobServiceClient blobServiceClient = hostStorageProvider.GetBlobServiceClient();
                    containerClient = blobServiceClient.GetBlobContainerClient(HostContainerNames.Hosts);
                }
                catch (Exception)
                {
                    return new InMemoryDistributedLockManager();
                }
            }

            var lockManager = new CloudBlobContainerDistributedLockManager(containerClient, loggerFactory);
            return lockManager;
        }

        public static IWebJobsBuilder AddAzureStorage(this IWebJobsBuilder builder)
        {
            builder.Services.TryAddSingleton<IConfigureOptions<HostStorageProvider>, HostStorageProviderOptions>();
            builder.Services.AddAzureClients(p => { });

            return builder;
        }
    }
}
