﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using Microsoft.Azure.WebJobs.Host.Blobs;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Azure.WebJobs.Host.TestCommon
{
    public static class JobHostFactory
    {
        public static TestJobHost<TProgram> Create<TProgram>()
        {
            return Create<TProgram>(CloudStorageAccount.DevelopmentStorageAccount, maxDequeueCount: 5);
        }

        public static TestJobHost<TProgram> Create<TProgram>(int maxDequeueCount)
        {
            return Create<TProgram>(CloudStorageAccount.DevelopmentStorageAccount, maxDequeueCount);
        }

        public static TestJobHost<TProgram> Create<TProgram>(CloudStorageAccount storageAccount)
        {
            return Create<TProgram>(storageAccount, maxDequeueCount : 5);
        }

        public static TestJobHost<TProgram> Create<TProgram>(CloudStorageAccount storageAccount, int maxDequeueCount)
        {
            IStorageAccountProvider storageAccountProvider = new SimpleStorageAccountProvider
            {
                StorageAccount = storageAccount,
                // use null logging string since unit tests don't need logs.
                DashboardAccount = null
            };
            IExtensionTypeLocator extensionTypeLocator = new NullExtensionTypeLocator();
            IHostIdProvider hostIdProvider = new FixedHostIdProvider("test");
            INameResolver nameResolver = null;
            IQueueConfiguration queueConfiguration = new SimpleQueueConfiguration(maxDequeueCount);
            ContextAccessor<IMessageEnqueuedWatcher> messageEnqueuedWatcherAccessor =
                new ContextAccessor<IMessageEnqueuedWatcher>();
            ContextAccessor<IBlobWrittenWatcher> blobWrittenWatcherAccessor =
                new ContextAccessor<IBlobWrittenWatcher>();
            ISharedContextProvider sharedContextProvider = new SharedContextProvider();
            IExtensionRegistry extensions = new DefaultExtensionRegistry();

            IFunctionOutputLoggerProvider outputLoggerProvider = new NullFunctionOutputLoggerProvider();
            var task = outputLoggerProvider.GetAsync(CancellationToken.None);
            task.Wait();
            IFunctionOutputLogger outputLogger = task.Result;
            IFunctionExecutor executor = new FunctionExecutor(new NullFunctionInstanceLogger(), outputLogger, BackgroundExceptionDispatcher.Instance);

            var triggerBindingProvider = DefaultTriggerBindingProvider.Create(
                    nameResolver, storageAccountProvider, extensionTypeLocator, hostIdProvider, queueConfiguration,
                    BackgroundExceptionDispatcher.Instance, messageEnqueuedWatcherAccessor, blobWrittenWatcherAccessor,
                    sharedContextProvider, extensions, TextWriter.Null);

            var bindingProvider = DefaultBindingProvider.Create(nameResolver, storageAccountProvider, extensionTypeLocator,
                        messageEnqueuedWatcherAccessor, blobWrittenWatcherAccessor, extensions);

            var functionIndexProvider = new FunctionIndexProvider(new FakeTypeLocator(typeof(TProgram)), triggerBindingProvider, bindingProvider, DefaultJobActivator.Instance, executor, new DefaultExtensionRegistry());

            IJobHostContextFactory contextFactory = new TestJobHostContextFactory
            {
                FunctionIndexProvider = functionIndexProvider,
                StorageAccountProvider = storageAccountProvider,
                Queues = queueConfiguration
            };

            IServiceProvider serviceProvider = new TestJobHostConfiguration
            {
                ContextFactory = contextFactory
            };

            return new TestJobHost<TProgram>(serviceProvider);
        }
    }
}
