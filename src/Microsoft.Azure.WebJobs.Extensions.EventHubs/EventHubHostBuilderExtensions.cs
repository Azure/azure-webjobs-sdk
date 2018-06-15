// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.Hosting
{
    public static class EventHubHostBuilderExtensions
    {
        public static IHostBuilder AddEventHubs(this IHostBuilder hostBuilder)
        {
            return hostBuilder
                .AddExtension<EventHubExtensionConfigProvider>()
                .ConfigureServices(services =>
                {
                    services.TryAddSingleton<EventHubConfiguration>();
                });
        }
    }
}
