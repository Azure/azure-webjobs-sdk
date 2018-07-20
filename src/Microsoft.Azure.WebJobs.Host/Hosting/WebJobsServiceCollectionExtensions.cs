// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Cancellation;
using Microsoft.Azure.WebJobs.Host.Bindings.Data;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Dispatch;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;


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
            services.TryAddSingleton<IExtensionRegistryFactory, DefaultExtensionRegistryFactory>();
            services.TryAddSingleton<IExtensionRegistry>(p => p.GetRequiredService<IExtensionRegistryFactory>().Create());

            // Type conversion
            services.TryAddSingleton<ITypeLocator, DefaultTypeLocator>();
            services.TryAddSingleton<IConverterManager, ConverterManager>();
            services.TryAddSingleton<IFunctionIndexProvider, FunctionIndexProvider>();
            services.TryAddSingleton<SingletonManager>();
            services.TryAddSingleton<IHostSingletonManager>(provider => provider.GetRequiredService<SingletonManager>());
            services.TryAddSingleton<SharedQueueHandler>();
            services.TryAddSingleton<IFunctionExecutor, FunctionExecutor>();
            services.TryAddSingleton<IJobHostContextFactory, JobHostContextFactory>();

            services.TryAddSingleton<ILoadbalancerQueue, InMemoryLoadbalancerQueue>();

            // Anybody can add IBindingProvider via DI. 
            // Consume the whole list via a CompositeBindingProvider
            services.TryAddSingleton<CompositeBindingProviderFactory>();
            services.TryAddSingleton<CompositeBindingProvider>(
                p => p.GetRequiredService<CompositeBindingProviderFactory>().Create());

            services.TryAddSingleton<ISharedContextProvider, SharedContextProvider>();

            services.TryAddSingleton<IJobHostMetadataProviderFactory, JobHostMetadataProviderFactory>();
            services.TryAddSingleton<IJobHostMetadataProvider>(p => p.GetService<IJobHostMetadataProviderFactory>().Create());
            services.TryAddSingleton<IExtensionTypeLocator, ExtensionTypeLocator>(); // $$$ remove

            // Empty logging. V1 Logging can replace this.              
            services.TryAddSingleton<ILegacyLogger, DisableLegacyLogger>(); // Gets replaced 
            services.TryAddSingleton<IFunctionOutputLogger, ConsoleFunctionOutputLogger>();
            services.TryAddSingleton<IFunctionInstanceLogger, FunctionInstanceLogger>();
            services.TryAddSingleton<IHostInstanceLogger, NullHostInstanceLogger>();


            // TODO: FACAVAL FIX THIS - Right now, We're only registering the FixedIdProvider
            // need to register the dynamic ID provider and verify if the logic in it can be improved (and have the storage dependency removed)
            // Tracked by https://github.com/Azure/azure-webjobs-sdk/issues/1802
            services.TryAddSingleton<IHostIdProvider, FixedHostIdProvider>();

            services.TryAddSingleton<IDistributedLockManager, InMemorySingletonManager>();


            // $$$ Can we remove these completely? 
            services.TryAddSingleton<DefaultTriggerBindingFactory>();
            services.TryAddSingleton<ITriggerBindingProvider>(p => p.GetRequiredService<DefaultTriggerBindingFactory>().Create());

            // Exception handler
            services.TryAddSingleton<IWebJobsExceptionHandlerFactory, DefaultWebJobsExceptionHandlerFactory>();
            services.TryAddSingleton<IWebJobsExceptionHandler>(p => p.GetRequiredService<IWebJobsExceptionHandlerFactory>().Create(p.GetRequiredService<IHost>()));

            services.TryAddSingleton<IConnectionStringProvider, AmbientConnectionStringProvider>();

            services.TryAddSingleton<INameResolver, DefaultNameResolver>();
            services.TryAddSingleton<IJobActivator, DefaultJobActivator>();

            // Event collector
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IEventCollectorProvider, FunctionResultAggregatorProvider>());
            services.TryAddSingleton<IEventCollectorFactory, EventCollectorFactory>();
            services.TryAddSingleton<IAsyncCollector<FunctionInstanceLogEntry>>(p => p.GetRequiredService<IEventCollectorFactory>().Create());

            // Options setup


            services.RegisterBuiltInBindings();

            // Core host services
            services.TryAddSingleton<IJobHost, JobHost>();

            return services;
        }

        public static IHostBuilder ConfigureWebJobsFastLogging(this IHostBuilder builder, IEventCollectorFactory fastLogger)
        {
            builder.ConfigureServices(services =>
            {
                services.AddWebJobsFastLogging(fastLogger);
            });
            return builder;
        }

        // This is an alternative to AddWebJobsLogging
        public static IServiceCollection AddWebJobsFastLogging(this IServiceCollection services, IEventCollectorFactory fastLogger)
        {
            services.AddSingleton<IFunctionOutputLoggerProvider, FastTableLoggerProvider>();
            services.AddSingleton<IFunctionOutputLogger, FastTableLoggerProvider>();

            services.AddSingleton(fastLogger);
            return services;
        }
        
        /// <summary>
        /// Adds builtin bindings 
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection RegisterBuiltInBindings(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            // for typeof(CancellationToken)
            services.AddSingleton<IBindingProvider, CancellationTokenBindingProvider>();

            // The TraceWriter binder handles all remaining TraceWriter/TextWriter parameters. It must come after the
            // Blob binding provider; otherwise bindings like Do([Blob("a/b")] TextWriter blob) wouldn't work.
            // for typeof(TraceWriter), typeof(TextWriter)
            services.AddSingleton<IBindingProvider, TraceWriterBindingProvider>();

            // for typeof(ILogger)
            services.AddSingleton<IBindingProvider, ILoggerBindingProvider>();

            // arbitrary binding to binding data 
            services.AddSingleton<IBindingProvider, DataBindingProvider>();



            return services;
        }
    }
}
