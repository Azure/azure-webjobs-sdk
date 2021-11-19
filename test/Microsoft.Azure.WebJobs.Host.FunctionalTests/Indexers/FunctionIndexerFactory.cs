// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    internal static class FunctionIndexerFactory
    {
        public static FunctionIndexer Create(INameResolver nameResolver = null,
            IExtensionRegistry extensionRegistry = null, ITriggerBindingProvider triggerBindingProvider = null, ILoggerFactory loggerFactory = null)
        {
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost(b =>
                {
                    b.UseHostId("testhost");

                    // Needed for Blob/Queue triggers and bindings
                    b.AddAzureStorageBlobs();
                    b.AddAzureStorageQueues();

                    b.AddServiceBus();
                })
                .ConfigureServices(services =>
                {
                    if (nameResolver != null)
                    {
                        services.AddSingleton<INameResolver>(nameResolver);
                    }

                    if (extensionRegistry != null)
                    {
                        services.AddSingleton<IExtensionRegistry>(extensionRegistry);
                    }
                })
                .Build();

            triggerBindingProvider = triggerBindingProvider ?? host.Services.GetService<ITriggerBindingProvider>();
            IBindingProvider bindingProvider = host.Services.GetService<CompositeBindingProvider>();
            IJobActivator activator = host.Services.GetService<IJobActivator>();
            extensionRegistry = host.Services.GetService<IExtensionRegistry>();
            SingletonManager singletonManager = host.Services.GetService<SingletonManager>();
            var serviceScopeFactory = host.Services.GetService<IServiceScopeFactory>();

            IFunctionExecutor executor = host.Services.GetService<IFunctionExecutor>();
            IConfiguration configuration = host.Services.GetService<IConfiguration>();

            // TODO: This should be using DI internally and not be so complicated to construct
            return new FunctionIndexer(triggerBindingProvider, bindingProvider, activator, executor, singletonManager, loggerFactory, configuration, serviceScopeFactory);
        }
    }
}