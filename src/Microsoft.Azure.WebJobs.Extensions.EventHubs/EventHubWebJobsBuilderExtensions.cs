// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.EventHubs.Processor;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.EventHubs;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.Hosting
{
    public static class EventHubWebJobsBuilderExtensions
    {
        public static IWebJobsBuilder AddEventHubs(this IWebJobsBuilder builder)
        {
            builder.AddExtension<EventHubExtensionConfigProvider>()
                .BindOptions<EventProcessorOptions>();

            builder.Services.TryAddSingleton<EventHubConfiguration>();

            return builder;
        }
    }
}
