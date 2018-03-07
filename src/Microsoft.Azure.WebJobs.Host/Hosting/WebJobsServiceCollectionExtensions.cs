// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Blobs;
using Microsoft.Azure.WebJobs.Host.Configuration;
using Microsoft.Azure.WebJobs.Host.Dispatch;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;


namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Extension methods for setting up WebJobs services in a <see cref="IServiceCollection" />.
    /// </summary>
    public static class WebJobsServiceCollectionExtensions
    {

        /// <summary>
        /// Adds the WebJobs services to the provided <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddWebJobs(this IServiceCollection services, Action<JobHostOptions> configure)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.Configure(configure);

            // A LOT of the service registrations below need to be cleaned up
            // maintaining some of the existing dependencies and model we previously had, 
            // but this should be reviewed as it can be improved.
            services.TryAddSingleton<IExtensionRegistry, DefaultExtensionRegistry>();
            
            // Type conversion
            services.TryAddSingleton<IExtensionTypeLocator, ExtensionTypeLocator>();
            services.TryAddSingleton<ITypeLocator>(p => new DefaultTypeLocator(p.GetRequiredService<IConsoleProvider>().Out, p.GetRequiredService<IExtensionRegistry>()));
            services.TryAddSingleton<IConverterManager, ConverterManager>();

            services.TryAddSingleton<SingletonManager>();
            services.TryAddSingleton<SharedQueueHandler>();
            services.TryAddSingleton<IFunctionExecutor, FunctionExecutor>();
            services.TryAddSingleton<IJobHostContextFactory, JobHostContextFactory>();
            services.TryAddSingleton<IFunctionInstanceLogger, FunctionInstanceLogger>();
            services.TryAddSingleton<IFunctionIndexProvider, FunctionIndexProvider>();
            services.TryAddSingleton<IBindingProviderFactory, DefaultBindingProvider>();
            services.TryAddSingleton<ISharedContextProvider, SharedContextProvider>();
            services.TryAddSingleton<IContextSetter<IMessageEnqueuedWatcher>>((p) => new ContextAccessor<IMessageEnqueuedWatcher>());
            services.TryAddSingleton<IContextSetter<IBlobWrittenWatcher>>((p) => new ContextAccessor<IBlobWrittenWatcher>());
            services.TryAddSingleton((p) => p.GetService<IContextSetter<IMessageEnqueuedWatcher>>() as IContextGetter<IMessageEnqueuedWatcher>);
            services.TryAddSingleton((p) => p.GetService<IContextSetter<IBlobWrittenWatcher>>() as IContextGetter<IBlobWrittenWatcher>);
            services.TryAddSingleton<IDistributedLockManagerFactory, DefaultDistributedLockManagerFactory>();
            services.TryAddSingleton<IDistributedLockManager>(p => p.GetRequiredService<IDistributedLockManagerFactory>().Create());

            services.AddWebJobsLogging();
            
            // TODO: FACAVAL FIX THIS - Right now, We're only registering the FixedIdProvider
            // need to register the dynamic ID provider and verify if the logic in it can be improved (and have the storage dependency removed)
            services.TryAddSingleton<IHostIdProvider, FixedHostIdProvider>();

            services.TryAddSingleton<DefaultTriggerBindingFactory>();
            services.TryAddSingleton<ITriggerBindingProvider>(p => p.GetRequiredService<DefaultTriggerBindingFactory>().Create());

            // Exception handler
            services.TryAddSingleton<IWebJobsExceptionHandlerFactory, DefaultWebJobsExceptionHandlerFactory>();
            services.TryAddSingleton<IWebJobsExceptionHandler>(p => p.GetRequiredService<IWebJobsExceptionHandlerFactory>().Create(p.GetRequiredService<IHost>()));

            // TODO: Remove passing the service provider here.
            services.TryAddSingleton<IStorageAccountProvider>(p => new DefaultStorageAccountProvider(p));
            services.TryAddSingleton<StorageClientFactory>();
            services.TryAddSingleton<INameResolver, DefaultNameResolver>();
            services.TryAddSingleton<IJobActivator, DefaultJobActivator>();
            services.TryAddSingleton<IFunctionResultAggregatorFactory, FunctionResultAggregatorFactory>();

            // Options setup
            services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<JobHostOptions>, JobHostOptionsSetup>());

            services.RegisterBuiltInBindings();

            // Core host services
            services.TryAddSingleton<IJobHost, JobHost>();

            return services;
        }

        private static IServiceCollection AddWebJobsLogging(this IServiceCollection services)
        {
            // Logging related services (lots of them...)
            services.TryAddSingleton<LoggerProviderFactory>();
            services.TryAddSingleton<IFunctionOutputLoggerProvider>(p => p.GetRequiredService<LoggerProviderFactory>().GetLoggerProvider<IFunctionOutputLoggerProvider>());
            services.TryAddSingleton<IFunctionInstanceLoggerProvider>(p => p.GetRequiredService<LoggerProviderFactory>().GetLoggerProvider<IFunctionInstanceLoggerProvider>());
            services.TryAddSingleton<IHostInstanceLoggerProvider>(p => p.GetRequiredService<LoggerProviderFactory>().GetLoggerProvider<IHostInstanceLoggerProvider>());
            services.TryAddSingleton<IConsoleProvider, DefaultConsoleProvider>();

            return services;
        }

        /// <summary>
        /// Adds the following bindings: <see cref="Tables.TableExtension"/>, <see cref="Queues.Bindings.QueueExtension"/>, 
        /// <see cref="Blobs.Bindings.BlobExtensionConfig"/> and <see cref="Blobs.Triggers.BlobTriggerExtensionConfig"/>.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection RegisterBuiltInBindings(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.TryAddSingleton<Host.Tables.TableExtension>();
            services.TryAddSingleton<Host.Queues.Bindings.QueueExtension>();
            services.TryAddSingleton<Host.Blobs.Bindings.BlobExtensionConfig>();
            services.TryAddSingleton<Host.Blobs.Triggers.BlobTriggerExtensionConfig>();

            return services;
        }
    }
}
