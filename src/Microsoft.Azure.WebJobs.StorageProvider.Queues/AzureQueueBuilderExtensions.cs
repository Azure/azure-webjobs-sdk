// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Azure.WebJobs.StorageProvider.Queues
{
    /// <summary>
    /// Extension methods for Storage Queues integration.
    /// </summary>
    public static class AzureQueueBuilderExtensions
    {
        /// <summary>
        /// Adds the Storage Queues extension to the provided <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to configure.</param>
        public static void AddAzureStorageQueues(this IServiceCollection services)
        {
            services.AddAzureClientsCore();
            services.TryAddSingleton<QueueServiceClientProvider>();
        }
    }
}
