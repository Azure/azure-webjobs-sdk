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
        public static IHostBuilder AddStorageForRuntimeInternals(this IHostBuilder builder)
        {
            return builder
               .ConfigureServices((context, services) =>
               {
                   // Add runtime services that depend on storage.
                   services.TryAddSingleton<BlobManagerXStorageAccountProvider>(); 
                   services.AddSingleton<IDistributedLockManager>(provider => Create(provider));
                                      
                   services.TryAddSingleton<IHostIdProvider, DynamicHostIdProvider>();

                   services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<LegacyConfig>, LegacyConfigSetup>());

                   // $$$
                   //services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<JobHostInternalStorageOptions>, JobHostInternalStorageOptionsSetup>());
               });
        }

        private static IDistributedLockManager Create(IServiceProvider provider)
        {
            var opts = provider.GetRequiredService<IOptions<LegacyConfig>>();

            // $$$ This is what DefaultDistributedLockManagerFactory used to do. 
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            ILogger logger = loggerFactory.CreateLogger<IDistributedLockManager>();

            var sas = provider.GetService<JobHostInternalStorageOptions>(); // may be null 
                        
            IDistributedLockManager lockManager;
            if (sas != null && sas.InternalContainer != null)            
            {
                lockManager = new BlobLeaseDistributedLockManager.SasContainer(sas.InternalContainer, logger);
            }
            else
            {
                var storageAccountProvider = provider.GetRequiredService<BlobManagerXStorageAccountProvider>();
                lockManager = new BlobLeaseDistributedLockManager.DedicatedStorage(storageAccountProvider, logger);
            }

            return lockManager;
        }
    }
}
