// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.Hosting
{
    public static class WebJobsHostBuilderExtensions
    {
        /// <summary>
        /// Applies WebJobs configuration to the specified <see cref="IHostBuilder"/>.
        /// </summary>
        /// <remarks>
        /// In addition to WebJobs service registrations, this method will also apply the following default configuration sources
        /// if they haven't already been specified:
        ///     - Json file ("appsettings.json")
        ///     - Environment variables
        /// </remarks>
        /// <param name="builder">The <see cref="IHostBuilder"/> to configure.</param>
        /// <returns>The <see cref="IHostBuilder"/>.</returns>
        public static IHostBuilder ConfigureWebJobs(this IHostBuilder builder)
        {
            return builder.ConfigureWebJobs(o => { }, o => { });
        }

        /// <summary>
        /// Applies WebJobs configuration to the specified <see cref="IHostBuilder"/>.
        /// </summary>
        /// <remarks>
        /// In addition to WebJobs service registrations, this method will also apply the following default configuration sources
        /// if they haven't already been specified:
        ///     - Json file ("appsettings.json")
        ///     - Environment variables
        /// </remarks>
        /// <param name="builder">The <see cref="IHostBuilder"/> to configure.</param>
        /// <param name="configure">Configuration action to perform as part of service configuration.</param>
        /// <returns>The <see cref="IHostBuilder"/>.</returns>
        public static IHostBuilder ConfigureWebJobs(this IHostBuilder builder, Action<IWebJobsBuilder> configure)
        {
            return builder.ConfigureWebJobs(configure, o => { });
        }

        /// <summary>
        /// Applies WebJobs configuration to the specified <see cref="IHostBuilder"/>.
        /// </summary>
        /// <remarks>
        /// In addition to WebJobs service registrations, this method will also apply the following default configuration sources
        /// if they haven't already been specified:
        ///     - Json file ("appsettings.json")
        ///     - Environment variables
        /// </remarks>
        /// <param name="builder">The <see cref="IHostBuilder"/> to configure.</param>
        /// <param name="configure">Configuration action to perform as part of service configuration.</param>
        /// <param name="configureOptions">Configuration action for <see cref="JobHostOptions"/>.</param>
        /// <returns>The <see cref="IHostBuilder"/>.</returns>
        public static IHostBuilder ConfigureWebJobs(this IHostBuilder builder, Action<IWebJobsBuilder> configure, Action<JobHostOptions> configureOptions)
        {
            return builder.ConfigureWebJobs((context, b) => configure(b), configureOptions);
        }

        /// <summary>
        /// Applies WebJobs configuration to the specified <see cref="IHostBuilder"/>.
        /// </summary>
        /// <remarks>
        /// In addition to WebJobs service registrations, this method will also apply the following default configuration sources
        /// if they haven't already been specified:
        ///     - Json file ("appsettings.json")
        ///     - Environment variables
        /// </remarks>
        /// <param name="builder">The <see cref="IHostBuilder"/> to configure.</param>
        /// <param name="configure">Configuration action to perform as part of service configuration.</param>
        /// <returns>The <see cref="IHostBuilder"/>.</returns>
        public static IHostBuilder ConfigureWebJobs(this IHostBuilder builder, Action<HostBuilderContext, IWebJobsBuilder> configure)
        {
            return builder.ConfigureWebJobs(configure, o => { });
        }

        /// <summary>
        /// Applies WebJobs configuration to the specified <see cref="IHostBuilder"/>.
        /// </summary>
        /// <remarks>
        /// In addition to WebJobs service registrations, this method will also apply the following default configuration sources
        /// if they haven't already been specified:
        ///     - Json file ("appsettings.json")
        ///     - Environment variables
        /// </remarks>
        /// <param name="builder">The <see cref="IHostBuilder"/> to configure.</param>
        /// <param name="configure">Configuration action to perform as part of service configuration.</param>
        /// <param name="configureOptions">Configuration action for <see cref="JobHostOptions"/>.</param>
        /// <returns>The <see cref="IHostBuilder"/>.</returns>
        public static IHostBuilder ConfigureWebJobs(this IHostBuilder builder, Action<HostBuilderContext, IWebJobsBuilder> configure, Action<JobHostOptions> configureOptions)
        {
            return ConfigureWebJobs(builder, configure, configureOptions, (c, b) => { });
        }

        /// <summary>
        /// Applies WebJobs configuration to the specified <see cref="IHostBuilder"/>.
        /// </summary>
        /// <remarks>
        /// In addition to WebJobs service registrations, this method will also apply the following default configuration sources
        /// if they haven't already been specified:
        ///     - Json file ("appsettings.json")
        ///     - Environment variables
        /// </remarks>
        /// <param name="builder">The <see cref="IHostBuilder"/> to configure.</param>
        /// <param name="configure">Configuration action to perform as part of service configuration.</param>
        /// <param name="configureOptions">Configuration action for <see cref="JobHostOptions"/>.</param>
        /// <param name="configureAppConfiguration">Additional action to perform as part of app configuration.</param>
        /// <returns>The <see cref="IHostBuilder"/>.</returns>
        public static IHostBuilder ConfigureWebJobs(this IHostBuilder builder, Action<HostBuilderContext, IWebJobsBuilder> configure,
            Action<JobHostOptions> configureOptions, Action<HostBuilderContext, IWebJobsConfigurationBuilder> configureAppConfiguration)
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.TryAddDefaultConfigurationSources();
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

        /// <summary>
        /// Configures the specified <see cref="IHostBuilder"/> as a scale manager host. This method is used for internal infrastructure only.
        /// </summary>
        /// <param name="builder">The <see cref="IHostBuilder"/> to configure.</param>
        /// <param name="configure">Configuration action to perform as part of service configuration.</param>
        /// <param name="configureScaleOptions">Configuration action for <see cref="ScaleOptions"/>.</param>
        /// <returns>The <see cref="IHostBuilder"/>.</returns>
        public static IHostBuilder ConfigureWebJobsScale(this IHostBuilder builder,
            Action<HostBuilderContext, IWebJobsBuilder> configure,
            Action<ScaleOptions> configureScaleOptions)
        {
            builder.ConfigureServices((context, services) =>
            {
                IWebJobsBuilder webJobsBuilder = services.AddWebJobsScale(configureScaleOptions);
                configure?.Invoke(context, webJobsBuilder);
            });

            return builder;
        }

        private static IConfigurationBuilder TryAddDefaultConfigurationSources(this IConfigurationBuilder config)
        {
            if (!config.Sources.OfType<JsonConfigurationSource>().Any(p => string.Equals(p.Path, "appsettings.json", StringComparison.OrdinalIgnoreCase)))
            {
                // only add the Json file provider if it hasn't been added already
                config.AddJsonFile("appsettings.json", optional: true);
            }

            if (!config.Sources.OfType<EnvironmentVariablesConfigurationSource>().Any())
            {
                // only add the environment variables provider if it hasn't been added already
                config.AddEnvironmentVariables();
            }

            return config;
        }
    }
}