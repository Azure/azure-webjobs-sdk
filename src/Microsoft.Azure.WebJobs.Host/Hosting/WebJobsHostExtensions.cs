// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
                services.Configure<JobHostOptions>(context.Configuration);

                services.AddWebJobs(configure);

                services.AddSingleton<IHostedService, JobHostService>();
            });

            return builder;
        }
    }
}
