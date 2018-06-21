// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    internal static class FunctionIndexerFactory
    {
        public class FakeStorageAccountProvider : StorageAccountProvider
        {
            private readonly XStorageAccount _account;

            public FakeStorageAccountProvider()
                : base(null)
            {
            }

            public override XStorageAccount Get(string name)
            {
                return XStorageAccount.New(CloudStorageAccount.DevelopmentStorageAccount);
            }
        }

        public static FunctionIndexer Create(CloudStorageAccount account = null, INameResolver nameResolver = null,
            IExtensionRegistry extensionRegistry = null, ILoggerFactory loggerFactory = null)
        {
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost()
                .AddAzureStorage()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<StorageAccountProvider>(new FakeStorageAccountProvider());


                    if (nameResolver != null)
                    {
                        services.AddSingleton<INameResolver>(nameResolver);
                    }

                    if (extensionRegistry != null)
                    {
                        services.AddSingleton<IExtensionRegistry>(extensionRegistry);
                    }
                })
                .AddAzureStorage()
                .Build();

            ITriggerBindingProvider triggerBindingProvider = host.Services.GetService<ITriggerBindingProvider>();
            IBindingProvider bindingProvider = host.Services.GetService<CompositeBindingProvider>();
            IJobActivator activator = host.Services.GetService<IJobActivator>();
            extensionRegistry = host.Services.GetService<IExtensionRegistry>();
            SingletonManager singletonManager = host.Services.GetService<SingletonManager>();

            IFunctionExecutor executor = host.Services.GetService<IFunctionExecutor>();

            // TODO: This should be using DI internally and not be so complicated to construct
            return new FunctionIndexer(triggerBindingProvider, bindingProvider, new DefaultJobActivator(), executor,
                extensionRegistry, singletonManager, loggerFactory);
        }

        /*
        private static IStorageAccountProvider GetStorageAccountProvider(CloudStorageAccount account)
        {
            StorageClientFactory clientFactory = new StorageClientFactory();

            IStorageAccount storageAccount = account != null ? new StorageAccount(account, clientFactory) : null;
            IStorageAccountProvider storageAccountProvider = new SimpleStorageAccountProvider(clientFactory)
            {
                StorageAccount = account
            };
            return storageAccountProvider;
        }*/
    }
}
