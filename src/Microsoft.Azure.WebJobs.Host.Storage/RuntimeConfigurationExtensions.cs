// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.WindowsAzure.Storage;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Configuration;
using WebJobs.Host.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Extensions.Hosting
{
    public static class RuntimeConfigurationExtensions
    {
        // WebJobs v1 Classic logging. Needed for dashboard.         
        // $$$ Update title? 
        public static IHostBuilder AddWebJobsLogging(this IHostBuilder builder)
        {
            return builder
               .ConfigureServices((context, services) =>
               {
                   services.AddWebJobsLogging();
               });
        }

        // WebJobs v1 Classic logging. Needed for dashboard.         
        public static IServiceCollection AddWebJobsLogging(this IServiceCollection services)
        {
            // Logging related services (lots of them...)
            services.TryAddSingleton<LoggerProviderFactory>();
                        
            services.TryAddSingleton<IFunctionOutputLoggerProvider>(p => p.GetRequiredService<LoggerProviderFactory>().GetLoggerProvider<IFunctionOutputLoggerProvider>());
            services.TryAddSingleton<IFunctionOutputLogger>(p => p.GetRequiredService<IFunctionOutputLoggerProvider>().GetAsync(CancellationToken.None).GetAwaiter().GetResult());

            services.TryAddSingleton<IFunctionInstanceLoggerProvider>(p => p.GetRequiredService<LoggerProviderFactory>().GetLoggerProvider<IFunctionInstanceLoggerProvider>());
            services.TryAddSingleton<IFunctionInstanceLogger>(p => p.GetRequiredService<IFunctionInstanceLoggerProvider>().GetAsync(CancellationToken.None).GetAwaiter().GetResult());

            services.TryAddSingleton<IHostInstanceLoggerProvider>(p => p.GetRequiredService<LoggerProviderFactory>().GetLoggerProvider<IHostInstanceLoggerProvider>());
            services.TryAddSingleton<IHostInstanceLogger>(p => p.GetRequiredService<IHostInstanceLoggerProvider>().GetAsync(CancellationToken.None).GetAwaiter().GetResult());

            return services;
        }

        // Make the Runtime itself use storage for its internal operations. 
        // Uses v1 app settings, via a LegacyConfigSetup object. 
        public static IHostBuilder AddAzureStorageCoreServices(this IHostBuilder builder)
        {
            return builder
               .ConfigureServices((context, services) =>
               {
                   // Replace existing runtime services with storage-backed implementations.
                   // Add runtime services that depend on storage.
                   services.AddSingleton<IDistributedLockManager>(provider => Create(provider));
                                      
                   services.TryAddSingleton<IHostIdProvider, DynamicHostIdProvider>();


                   // Used specifically for the CloudBlobContainerDistributedLockManager implementaiton 
                   services.TryAddSingleton<DistributedLockManagerContainerProvider>();

                   services.TryAddEnumerable(
                       ServiceDescriptor.Transient<IConfigureOptions<LegacyConfig>, LegacyConfigSetup>());

                   services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<JobHostInternalStorageOptions>, JobHostInternalStorageOptionsSetup>());
               });
        }

        // This is only called if the host didn't already provide an implementation 
        private static IDistributedLockManager Create(IServiceProvider provider)
        {
            // $$$ get rid of LegacyConfig
            var opts = provider.GetRequiredService<IOptions<LegacyConfig>>();

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
