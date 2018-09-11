// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Azure.WebJobs.ServiceBus.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.Hosting
{
    public static class ServiceBusHostBuilderExtensions
    {
        public static IWebJobsBuilder AddServiceBus(this IWebJobsBuilder builder)
        {
            builder.AddExtension<ServiceBusExtensionConfigProvider>()
                .ConfigureOptions<ServiceBusOptions>((config, path, options) =>
                {
                    options.ConnectionString = config.GetConnectionString(Constants.DefaultConnectionStringName);

                    IConfigurationSection section = config.GetSection(path);
                    section.Bind(options);
                });

            builder.Services.TryAddSingleton<MessagingProvider>();

            return builder;
        }
    }
}
