// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Moq;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    internal static class FunctionIndexerFactory
    {
        public static FunctionIndexer Create(CloudStorageAccount account = null, INameResolver nameResolver = null,
            IExtensionRegistry extensionRegistry = null, ILoggerFactory loggerFactory = null)
        {
            IStorageAccountProvider storageAccountProvider = GetStorageAccountProvider(account);

            var config = TestHelpers.NewConfig(storageAccountProvider, nameResolver, extensionRegistry);
            var services = config.CreateStaticServices();

            ITriggerBindingProvider triggerBindingProvider = services.GetService<ITriggerBindingProvider>();
            IBindingProvider bindingProvider = services.GetService<IBindingProvider>();
            IJobActivator activator = services.GetService<IJobActivator>();
            extensionRegistry = services.GetService<IExtensionRegistry>();

            SingletonManager singletonManager = new SingletonManager();
            IWebJobsExceptionHandler exceptionHandler = new WebJobsExceptionHandler(new Mock<IHost>().Object);
            IFunctionOutputLoggerProvider outputLoggerProvider = new NullFunctionOutputLoggerProvider();

            IFunctionExecutor executor = new FunctionExecutor(new NullFunctionInstanceLogger(), outputLoggerProvider, exceptionHandler, loggerFactory: loggerFactory);

            return new FunctionIndexer(triggerBindingProvider, bindingProvider, new DefaultJobActivator(), executor,
                extensionRegistry, singletonManager, loggerFactory);
        }

        private static IStorageAccountProvider GetStorageAccountProvider(CloudStorageAccount account)
        {
            Mock<IServiceProvider> services = new Mock<IServiceProvider>(MockBehavior.Strict);
            StorageClientFactory clientFactory = new StorageClientFactory();
            services.Setup(p => p.GetService(typeof(StorageClientFactory))).Returns(clientFactory);
            IStorageAccount storageAccount = account != null ? new StorageAccount(account, services.Object) : null;
            IStorageAccountProvider storageAccountProvider = new SimpleStorageAccountProvider(services.Object)
            {
                StorageAccount = account
            };
            return storageAccountProvider;
        }
    }
}
