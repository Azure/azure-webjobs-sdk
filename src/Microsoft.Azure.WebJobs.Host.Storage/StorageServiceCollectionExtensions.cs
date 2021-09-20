// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
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

            // May need to rename this to HostBlobServiceClientProvider
            services.TryAddSingleton<BlobServiceClientProvider>();
            services.AddAzureClientsCore();

            services.AddSingleton<IConcurrencyStatusRepository, BlobStorageConcurrencyStatusRepository>();
        }

        // This is only called if the host didn't already provide an implementation
        // TODO: Should this attempt to create a client or just check for connection setting?
        private static IDistributedLockManager Create(IServiceProvider provider)
        {
            var azureStorageProvider = provider.GetRequiredService<IAzureStorageProvider>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<IDistributedLockManager>();
            try
            {
                var container = azureStorageProvider.GetWebJobsBlobContainerClient();
                logger.LogDebug("Using BlobLeaseDistributedLockManager in Functions Host.");
                return new BlobLeaseDistributedLockManager(loggerFactory, azureStorageProvider);
            }
            catch (InvalidOperationException)
            {
                // If there is an error getting the container client,
                // register an InMemoryDistributedLockManager.
                logger.LogDebug("Using InMemoryDistributedLockManager in Functions Host.");
                return new InMemoryDistributedLockManager();
            }
        }
    }
}
