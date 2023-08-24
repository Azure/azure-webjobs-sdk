// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.Hosting
{
    public static class StorageServiceCollectionExtensions
    {
        public static void AddAzureStorageCoreServices(this IServiceCollection services)
        {
            // Replace existing runtime services with storage-backed implementations.
            // Add runtime services that depend on storage.
            services.AddSingleton<IDistributedLockManager>(provider => Create(provider));

            services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<JobHostInternalStorageOptions>, CoreWebJobsOptionsSetup<JobHostInternalStorageOptions>>());

            services.TryAddSingleton<IDelegatingHandlerProvider, DefaultDelegatingHandlerProvider>();

            // Adds necessary Azure services to create clients
            services.AddAzureClientsCore();

            services.TryAddSingleton<IAzureBlobStorageProvider, AzureStorageProvider>();

            services.AddSingleton<IConcurrencyStatusRepository, BlobStorageConcurrencyStatusRepository>();
        }

        public static void AddAzureStorageScaleServices(this IServiceCollection services)
        {
            services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<JobHostInternalStorageOptions>, CoreWebJobsOptionsSetup<JobHostInternalStorageOptions>>());
            services.TryAddSingleton<IAzureBlobStorageProvider, AzureStorageProvider>();
            services.AddSingleton<IConcurrencyStatusRepository, BlobStorageConcurrencyStatusRepository>();
        }

        // This is only called if the host didn't already provide an implementation
        private static IDistributedLockManager Create(IServiceProvider provider)
        {
            var blobStorageProvider = provider.GetRequiredService<IAzureBlobStorageProvider>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<IDistributedLockManager>();

            IDistributedLockManager lockManager;
            if (blobStorageProvider.TryCreateHostingBlobContainerClient(out _))
            {
                lockManager = new BlobLeaseDistributedLockManager(loggerFactory, blobStorageProvider);
            }
            else
            {
                // If there is an error getting the container client,
                // register an InMemoryDistributedLockManager.
                lockManager = new InMemoryDistributedLockManager();
            }

            logger.LogDebug($"Using {lockManager.GetType().Name}");
            return lockManager;
        }
    }
}
