// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Hosting
{
    public static class WebJobsHostExtensions
    {
        public static IHostBuilder ConfigureWebJobsHost(this IHostBuilder builder)
        {
            return builder.ConfigureWebJobsHost(o => { });
        }

        public static IHostBuilder ConfigureWebJobsHost(this IHostBuilder builder, Action<JobHostOptions> configure)
        {
            builder.ConfigureServices((context, services) =>
            {
                services.Configure(configure);

                // Temporary... this needs to be removed and JobHostConfiguration needs to have settings
                // moved to the appropriate options implementation and all services registered through DI
                services.AddSingleton(p => new JobHostConfiguration(p.GetRequiredService<ILoggerFactory>()));

                services.AddSingleton<IExtensionRegistry, DefaultExtensionRegistry>();
                services.AddSingleton<IConsoleProvider, DefaultConsoleProvider>();
                services.AddSingleton<ITypeLocator>(p => new DefaultTypeLocator(p.GetRequiredService<IConsoleProvider>().Out, p.GetRequiredService<IExtensionRegistry>()));
                services.AddSingleton<IConverterManager, ConverterManager>();
                services.AddSingleton<IWebJobsExceptionHandler, WebJobsExceptionHandler>();

                services.AddSingleton<IQueueConfiguration, JobHostQueuesConfiguration>();

                // TODO: Remove passing the service provider here.
                services.AddSingleton<IStorageAccountProvider>(p => new DefaultStorageAccountProvider(p));
                services.AddSingleton<StorageClientFactory, StorageClientFactory>();
                services.AddSingleton<INameResolver, DefaultNameResolver>();
                services.AddSingleton<IJobActivator, DefaultJobActivator>();
                services.AddSingleton<IFunctionResultAggregatorFactory, FunctionResultAggregatorFactory>();
                services.AddSingleton<IHostedService, JobHostService>();
                services.AddSingleton<IJobHost, JobHost>();
            });

            return builder;
        }
    }
}
