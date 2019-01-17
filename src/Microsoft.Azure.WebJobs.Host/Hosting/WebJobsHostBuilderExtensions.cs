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
    public static class WebJobsHostBuilderExtensions
    {
        public static IHostBuilder ConfigureWebJobs(this IHostBuilder builder)
        {
            return builder.ConfigureWebJobs(o => { }, o => { });
        }

        public static IHostBuilder ConfigureWebJobs(this IHostBuilder builder, Action<IWebJobsBuilder> configure)
        {
            return builder.ConfigureWebJobs(configure, o => { });
        }

        public static IHostBuilder ConfigureWebJobs(this IHostBuilder builder, Action<IWebJobsBuilder> configure, Action<JobHostOptions> configureOptions)
        {
            builder.ConfigureAppConfiguration((hostingContext, config) =>
            {
                var env = hostingContext.HostingEnvironment;

                config.AddJsonFile("appsettings.json", optional: true)
                      .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

                config.AddEnvironmentVariables();
            });

            builder.ConfigureServices((context, services) =>
            {
                IWebJobsBuilder webJobsBuilder = services.AddWebJobs(configureOptions);
                configure(webJobsBuilder);

                services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, JobHostService>());
            });

            return builder;
        }
    }
}
