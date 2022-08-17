// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.Hosting
{
    public static class WebJobsHostBuilderExtensions
    {
        /// <summary>
        /// Configure WebJobs support for the host.
        /// </summary>
        /// <param name="builder">The <see cref="IHostBuilder"/> to configure.</param>
        /// <param name="withDefaultAppConfiguration">Indicates whether default configuration sources should be added. The default is true.
        /// If you're calling <see cref="IHostBuilder.ConfigureAppConfiguration(Action{HostBuilderContext, IConfigurationBuilder})"/> and
        /// want to fully control configuration, specify false.</param>
        /// <returns></returns>
        public static IHostBuilder ConfigureWebJobs(this IHostBuilder builder, bool withDefaultAppConfiguration = true)
        {
            return builder.ConfigureWebJobs(o => { }, o => { }, withDefaultAppConfiguration);
        }

        /// <summary>
        /// Configure WebJobs support for the host.
        /// </summary>
        /// <param name="builder">The <see cref="IHostBuilder"/> to configure.</param>
        /// <param name="configure">Action to perform WebJobs configuration.</param>
        /// <param name="withDefaultAppConfiguration">Indicates whether default configuration sources should be added. The default is true.
        /// If you're calling <see cref="IHostBuilder.ConfigureAppConfiguration(Action{HostBuilderContext, IConfigurationBuilder})"/> and
        /// want to fully control configuration, specify false.</param>
        /// <returns></returns>
        public static IHostBuilder ConfigureWebJobs(this IHostBuilder builder, Action<IWebJobsBuilder> configure, bool withDefaultAppConfiguration = true)
        {
            return builder.ConfigureWebJobs(configure, o => { }, withDefaultAppConfiguration);
        }

        /// <summary>
        /// Configure WebJobs support for the host.
        /// </summary>
        /// <param name="builder">The <see cref="IHostBuilder"/> to configure.</param>
        /// <param name="configure">Action to perform WebJobs configuration.</param>
        /// <param name="configureOptions">Action to configure <see cref="JobHostOptions"/>.</param>
        /// <param name="withDefaultAppConfiguration">Indicates whether default configuration sources should be added. The default is true.
        /// If you're calling <see cref="IHostBuilder.ConfigureAppConfiguration(Action{HostBuilderContext, IConfigurationBuilder})"/> and
        /// want to fully control configuration, specify false.</param>
        /// <returns></returns>
        public static IHostBuilder ConfigureWebJobs(this IHostBuilder builder, Action<IWebJobsBuilder> configure, Action<JobHostOptions> configureOptions, bool withDefaultAppConfiguration = true)
        {
            return builder.ConfigureWebJobs((context, b) => configure(b), configureOptions, withDefaultAppConfiguration);
        }

        /// <summary>
        /// Configure WebJobs support for the host.
        /// </summary>
        /// <param name="builder">The <see cref="IHostBuilder"/> to configure.</param>
        /// <param name="configure">Action to perform WebJobs configuration.</param>
        /// <param name="withDefaultAppConfiguration">Indicates whether default configuration sources should be added. The default is true.
        /// If you're calling <see cref="IHostBuilder.ConfigureAppConfiguration(Action{HostBuilderContext, IConfigurationBuilder})"/> and
        /// want to fully control configuration, specify false.</param>
        /// <returns></returns>
        public static IHostBuilder ConfigureWebJobs(this IHostBuilder builder, Action<HostBuilderContext, IWebJobsBuilder> configure, bool withDefaultAppConfiguration = true)
        {
            return builder.ConfigureWebJobs(configure, o => { }, withDefaultAppConfiguration);
        }

        /// <summary>
        /// Configure WebJobs support for the host.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        /// <param name="configureOptions"></param>
        /// <param name="withDefaultAppConfiguration"></param>
        /// <returns></returns>
        public static IHostBuilder ConfigureWebJobs(this IHostBuilder builder, Action<HostBuilderContext, IWebJobsBuilder> configure, Action<JobHostOptions> configureOptions, bool withDefaultAppConfiguration = true)
        {
            return ConfigureWebJobs(builder, configure, configureOptions, (c, b) => { }, withDefaultAppConfiguration);
        }

        /// <summary>
        /// Configure WebJobs support for the host.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        /// <param name="configureOptions"></param>
        /// <param name="configureAppConfiguration"></param>
        /// <param name="withDefaultAppConfiguration"></param>
        /// <returns></returns>
        public static IHostBuilder ConfigureWebJobs(this IHostBuilder builder, Action<HostBuilderContext, IWebJobsBuilder> configure,
            Action<JobHostOptions> configureOptions, Action<HostBuilderContext, IWebJobsConfigurationBuilder> configureAppConfiguration, bool withDefaultAppConfiguration = true)
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                if (withDefaultAppConfiguration)
                {
                    config.AddJsonFile("appsettings.json", optional: true);
                    config.AddEnvironmentVariables();
                }

                configureAppConfiguration?.Invoke(context, new WebJobsConfigurationBuilder(config));
            });

            builder.ConfigureServices((context, services) =>
            {
                IWebJobsBuilder webJobsBuilder = services.AddWebJobs(configureOptions);
                configure?.Invoke(context, webJobsBuilder);

                services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, JobHostService>());
            });

            return builder;
        }
    }
}