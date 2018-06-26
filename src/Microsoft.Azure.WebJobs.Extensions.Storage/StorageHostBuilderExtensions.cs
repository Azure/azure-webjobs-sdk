// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.StorageAccount;
using Microsoft.Azure.WebJobs.Host.Blobs;
using Microsoft.Azure.WebJobs.Host.Blobs.Bindings;
using Microsoft.Azure.WebJobs.Host.Blobs.Triggers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Host.Queues.Bindings;
using Microsoft.Azure.WebJobs.Host.Queues.Listeners;
using Microsoft.Azure.WebJobs.Host.Queues.Triggers;
using Microsoft.Azure.WebJobs.Host.Tables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.WindowsAzure.Storage;
using WebJobs.Extensions.Storage;

namespace Microsoft.Extensions.Hosting
{
    public static class StorageHostBuilderExtensions
    {
        // $$$ Extensions need some way to register services!  Callable from Script (so not via an explicit Extension method) 

        public static IHostBuilder AddAzureStorage(this IHostBuilder builder)
        {
            // add webjobs to user agent for all storage calls
            OperationContext.GlobalSendingRequest += (sender, e) =>
            {
                // TODO: FACAVAL - This is not supported on by the latest version of the
                // storage SDK. Need to re-add this when the capability is reintroduced.
                // e.UserAgent += " AzureWebJobs";
            }; 

            return builder
                .ConfigureServices((context, services) =>
                {
                    // $$$ Move to Host.Storage? 
                    services.TryAddSingleton<ILoadbalancerQueue, StorageLoadbalancerQueue>();

                    services.TryAddSingleton<SharedQueueWatcher>();

                    // $$$ Remove this, should be done via DI 
                    services.TryAddSingleton<ISharedContextProvider, SharedContextProvider>();

                    services.TryAddSingleton<StorageAccountProvider>();

                    services.TryAddSingleton<IContextSetter<IBlobWrittenWatcher>>((p) => new ContextAccessor<IBlobWrittenWatcher>());
                    services.TryAddSingleton((p) => p.GetService<IContextSetter<IBlobWrittenWatcher>>() as IContextGetter<IBlobWrittenWatcher>);

                    services.TryAddSingleton<IContextSetter<IMessageEnqueuedWatcher>>((p) => new ContextAccessor<IMessageEnqueuedWatcher>());
                    services.TryAddSingleton((p) => p.GetService<IContextSetter<IMessageEnqueuedWatcher>>() as IContextGetter<IMessageEnqueuedWatcher>);

                    services.TryAddSingleton<BlobTriggerAttributeBindingProvider>();

                    services.TryAddSingleton<QueueTriggerAttributeBindingProvider>();

                    services.TryAddEnumerable(ServiceDescriptor.Singleton<IBindingProvider, CloudStorageAccountBindingProvider>());
                })

                .AddExtension<TableExtension>()

                .AddExtension<QueueExtension>()

                .AddExtension<BlobExtensionConfig>()
                .AddExtension<BlobTriggerExtensionConfig>();
        }
    }
}
