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
    }
}
