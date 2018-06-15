// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.Hosting
{
    public static class WebJobsHostExtensions
    {
        public static IHostBuilder ConfigureWebJobsHost(this IHostBuilder builder)
        {
            return builder.ConfigureWebJobsHost(o => { });
        }

        public static IHostBuilder ConfigureWebJobsHost(this IHostBuilder builder, Action<JobHostOptions> configure)
        {
            builder.ConfigureAppConfiguration(config =>
            {
                config.AddJsonFile("appsettings.json", optional: true);
                config.AddEnvironmentVariables();
            });

            builder.ConfigureServices((context, services) =>
            {
                // TODO: FACAVAL
                // services.Configure<JobHostOptions>(context.Configuration);

                services.AddWebJobs(configure);

                services.AddSingleton<IHostedService, JobHostService>();
            });

            return builder;
        }

        public static IHostBuilder AddExtension<TExtension>(this IHostBuilder builder)
            where TExtension : class, IExtensionConfigProvider
        {
            builder.ConfigureServices(services =>
            {
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IExtensionConfigProvider, TExtension>());
            });

            return builder;
        }

        public static IHostBuilder AddExtension(this IHostBuilder builder, IExtensionConfigProvider instance)
        {
            builder.ConfigureServices(services =>
            {
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IExtensionConfigProvider>(instance));
            });

            return builder;
        }

        public static IHostBuilder UseWebJobsStartup<T>(this IHostBuilder builder) where T : IWebJobsStartup, new()
        {
            return builder.UseWebJobsStartup(typeof(T));
        }

        public static IHostBuilder UseWebJobsStartup(this IHostBuilder builder, Type startupType)
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
        /// <param name="builder">The <see cref="IHostBuilder"/> instance to configure.</param>
        /// <returns>The updated <see cref="IHostBuilder"/> instance.</returns>
        public static IHostBuilder UseExternalStartup(this IHostBuilder builder)
        {
            return builder.UseExternalStartup(new DefaultStartupTypeDiscoverer());
        }

        /// <summary>
        /// Enables use of external configuration providers, allowing them to inject services and update
        /// configuration during the host initialization process.
        /// </summary>
        /// <param name="builder">The <see cref="IHostBuilder"/> instance to configure.</param>
        /// <param name="typeDiscoverer">An implementation of <see cref="IWebJobsStartupTypeDiscoverer"/> that provides a list of types that 
        /// should be used in the startup process.</param>
        /// <returns>The updated <see cref="IHostBuilder"/> instance.</returns>
        public static IHostBuilder UseExternalStartup(this IHostBuilder builder, IWebJobsStartupTypeDiscoverer typeDiscoverer)
        {
            Type[] types = typeDiscoverer.GetStartupTypes();

            foreach (var type in types)
            {
                builder.UseWebJobsStartup(type);
            }

            return builder;
        }
    }
}
