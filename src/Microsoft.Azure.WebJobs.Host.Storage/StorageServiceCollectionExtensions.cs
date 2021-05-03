// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebJobs.Host.Storage.Logging;

namespace Microsoft.Extensions.Hosting
{
    public static class StorageServiceCollectionExtensions
    {
        [Obsolete("Dashboard is being deprecated. Use AppInsights.")]
        public static IServiceCollection AddDashboardLogging(this IServiceCollection services)
        {
            services.TryAddSingleton<LoggerProviderFactory>();

            services.TryAddSingleton<IFunctionOutputLoggerProvider>(p => p.GetRequiredService<LoggerProviderFactory>().GetLoggerProvider<IFunctionOutputLoggerProvider>());
            services.TryAddSingleton<IFunctionOutputLogger>(p => p.GetRequiredService<IFunctionOutputLoggerProvider>().GetAsync(CancellationToken.None).GetAwaiter().GetResult());

            services.TryAddSingleton<IFunctionInstanceLoggerProvider>(p => p.GetRequiredService<LoggerProviderFactory>().GetLoggerProvider<IFunctionInstanceLoggerProvider>());
            services.TryAddSingleton<IFunctionInstanceLogger>(p => p.GetRequiredService<IFunctionInstanceLoggerProvider>().GetAsync(CancellationToken.None).GetAwaiter().GetResult());

            services.TryAddSingleton<IHostInstanceLoggerProvider>(p => p.GetRequiredService<LoggerProviderFactory>().GetLoggerProvider<IHostInstanceLoggerProvider>());
            services.TryAddSingleton<IHostInstanceLogger>(p => p.GetRequiredService<IHostInstanceLoggerProvider>().GetAsync(CancellationToken.None).GetAwaiter().GetResult());

            return services;
        }

        public static void AddAzureStorageCoreServices(this IServiceCollection services)
        {
            // Replace existing runtime services with storage-backed implementations.
            // Add runtime services that depend on storage.
            services.AddSingleton<IDistributedLockManager>(provider => Create(provider));

            // Used specifically for the CloudBlobContainerDistributedLockManager implementation
            services.TryAddSingleton<DistributedLockManagerContainerProvider>();

            services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<StorageAccountOptions>, StorageAccountOptionsSetup>());
            services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<JobHostInternalStorageOptions>, CoreWebJobsOptionsSetup<JobHostInternalStorageOptions>>());

            services.TryAddSingleton<IDelegatingHandlerProvider, DefaultDelegatingHandlerProvider>();
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
                    return new InMemoryDistributedLockManager();
                }

                var blobClient = new CloudBlobClient(account.BlobStorageUri, account.Credentials, config.DelegatingHandlerProvider?.Create());
                container = blobClient.GetContainerReference(HostContainerNames.Hosts);
            }

            var lockManager = new CloudBlobContainerDistributedLockManager(container, loggerFactory);
            return lockManager;
        }
    }
}
