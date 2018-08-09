// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Cancellation;
using Microsoft.Azure.WebJobs.Host.Bindings.Data;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Hosting;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Azure.WebJobs
{
    public static class WebJobsBuilderExtensions
    {
        public static IWebJobsExtensionBuilder AddExtension<TExtension>(this IWebJobsBuilder builder)
          where TExtension : class, IExtensionConfigProvider
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IExtensionConfigProvider, TExtension>());

            string extensionName = Utility.GetExtensionName<TExtension>();
            return new WebJobsExtensionBuilder(builder.Services, extensionName);
        }

        public static IWebJobsExtensionBuilder AddExtension(this IWebJobsBuilder builder, IExtensionConfigProvider instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IExtensionConfigProvider>(instance));

            string extensionName = Utility.GetExtensionName(instance);
            return new WebJobsExtensionBuilder(builder.Services, extensionName);
        }

        public static IWebJobsBuilder AddExtension(this IWebJobsBuilder builder, Type extensionConfigProviderType)
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IExtensionConfigProvider), extensionConfigProviderType));

            return builder;
        }

        public static IWebJobsBuilder UseHostId(this IWebJobsBuilder builder, string hostId)
        {
            if (!HostIdValidator.IsValid(hostId))
            {
                throw new InvalidOperationException(HostIdValidator.ValidationMessage);
            }

            builder.Services.AddSingleton<IHostIdProvider>(new FixedHostIdProvider(hostId));

            return builder;
        }

        public static IWebJobsBuilder UseWebJobsStartup<T>(this IWebJobsBuilder builder) where T : IWebJobsStartup, new()
        {
            return builder.UseWebJobsStartup(typeof(T));
        }

        public static IWebJobsBuilder UseWebJobsStartup(this IWebJobsBuilder builder, Type startupType)
        {
            if (!typeof(IWebJobsStartup).IsAssignableFrom(startupType))
            {
                throw new ArgumentException($"The {nameof(startupType)} argument must be an implementation of {typeof(IWebJobsStartup).FullName}");
            }

            IWebJobsStartup startup = (IWebJobsStartup)Activator.CreateInstance(startupType);
            startup.Configure(builder);

            return builder;
        }

        /// <summary>
        /// Enables use of external configuration providers, allowing them to inject services and update
        /// configuration during the host initialization process.
        /// Type discovery is performed using the <see cref="DefaultStartupTypeDiscoverer"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> instance to configure.</param>
        /// <returns>The updated <see cref="IHostBuilder"/> instance.</returns>
        public static IWebJobsBuilder UseExternalStartup(this IWebJobsBuilder builder)
        {
            return builder.UseExternalStartup(new DefaultStartupTypeDiscoverer());
        }

        /// <summary>
        /// Enables use of external configuration providers, allowing them to inject services and update
        /// configuration during the host initialization process.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> instance to configure.</param>
        /// <param name="typeDiscoverer">An implementation of <see cref="IWebJobsStartupTypeDiscoverer"/> that provides a list of types that 
        /// should be used in the startup process.</param>
        /// <returns>The updated <see cref="IHostBuilder"/> instance.</returns>
        public static IWebJobsBuilder UseExternalStartup(this IWebJobsBuilder builder, IWebJobsStartupTypeDiscoverer typeDiscoverer)
        {
            Type[] types = typeDiscoverer.GetStartupTypes();

            foreach (var type in types)
            {
                builder.UseWebJobsStartup(type);
            }

            return builder;
        }

        // This is an alternative to AddWebJobsLogging
        public static IWebJobsBuilder AddFastLogging(this IWebJobsBuilder builder, IEventCollectorFactory fastLogger)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (fastLogger == null)
            {
                throw new ArgumentNullException(nameof(fastLogger));
            }

            builder.Services.AddSingleton<IFunctionOutputLoggerProvider, FastTableLoggerProvider>();
            builder.Services.AddSingleton<IFunctionOutputLogger, FastTableLoggerProvider>();

            builder.Services.AddSingleton(fastLogger);
            return builder;
        }

        /// <summary>
        /// Adds the ability to bind to an <see cref="ExecutionContext"/> from a WebJobs function.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <param name="configure">An optional <see cref="Action{ExecutionContextBindingOptions}"/> to configure the provided <see cref="ExecutionContextOptions"/>.</param>
        /// <returns></returns>
        public static IWebJobsBuilder AddExecutionContextBinding(this IWebJobsBuilder builder, Action<ExecutionContextOptions> configure = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.Services.AddSingleton<IBindingProvider, ExecutionContextBindingProvider>();

            if (configure != null)
            {
                builder.Services.Configure(configure);
            }

            return builder;
        }

        /// <summary>
        /// Adds builtin bindings 
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IWebJobsBuilder AddBuiltInBindings(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            // for typeof(CancellationToken)
            builder.Services.AddSingleton<IBindingProvider, CancellationTokenBindingProvider>();

            // The TraceWriter binder handles all remaining TraceWriter/TextWriter parameters. It must come after the
            // Blob binding provider; otherwise bindings like Do([Blob("a/b")] TextWriter blob) wouldn't work.
            // for typeof(TraceWriter), typeof(TextWriter)
            builder.Services.AddSingleton<IBindingProvider, TraceWriterBindingProvider>();

            // for typeof(ILogger)
            builder.Services.AddSingleton<IBindingProvider, ILoggerBindingProvider>();

            // arbitrary binding to binding data 
            builder.Services.AddSingleton<IBindingProvider, DataBindingProvider>();

            return builder;
        }
    }
}
