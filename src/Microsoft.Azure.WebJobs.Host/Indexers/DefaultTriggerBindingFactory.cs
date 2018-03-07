// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Blobs;
using Microsoft.Azure.WebJobs.Host.Blobs.Triggers;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Host.Queues.Triggers;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Host.Indexers
{
    internal class DefaultTriggerBindingFactory
    {
        private readonly INameResolver _nameResolver;
        private readonly IStorageAccountProvider _storageAccountProvider;
        private readonly IExtensionTypeLocator _extensionTypeLocator;
        private readonly IHostIdProvider _hostIdProvider;
        private readonly IOptions<JobHostQueuesOptions> _queueConfiguration;
        private readonly IOptions<JobHostBlobsOptions> _blobsConfiguration;
        private readonly IWebJobsExceptionHandler _exceptionHandler;
        private readonly IContextSetter<IMessageEnqueuedWatcher> _messageEnqueuedWatcherSetter;
        private readonly IContextSetter<IBlobWrittenWatcher> _blobWrittenWatcherSetter;
        private readonly ISharedContextProvider _sharedContextProvider;
        private readonly IExtensionRegistry _extensions;
        private readonly SingletonManager _singletonManager;
        private readonly ILoggerFactory _loggerFactory;

        public DefaultTriggerBindingFactory(INameResolver nameResolver,
            IStorageAccountProvider storageAccountProvider,
            IExtensionTypeLocator extensionTypeLocator,
            IHostIdProvider hostIdProvider,
            IOptions<JobHostQueuesOptions> queueConfiguration,
            IOptions<JobHostBlobsOptions> blobsConfiguration,
            IWebJobsExceptionHandler exceptionHandler,
            IContextSetter<IMessageEnqueuedWatcher> messageEnqueuedWatcherSetter,
            IContextSetter<IBlobWrittenWatcher> blobWrittenWatcherSetter,
            ISharedContextProvider sharedContextProvider,
            IExtensionRegistry extensions,
            SingletonManager singletonManager,
            ILoggerFactory loggerFactory)
        {
            _nameResolver = nameResolver ?? throw new System.ArgumentNullException(nameof(nameResolver));
            _storageAccountProvider = storageAccountProvider ?? throw new System.ArgumentNullException(nameof(storageAccountProvider));
            _extensionTypeLocator = extensionTypeLocator ?? throw new System.ArgumentNullException(nameof(extensionTypeLocator));
            _hostIdProvider = hostIdProvider ?? throw new System.ArgumentNullException(nameof(hostIdProvider));
            _queueConfiguration = queueConfiguration ?? throw new System.ArgumentNullException(nameof(queueConfiguration));
            _blobsConfiguration = blobsConfiguration ?? throw new System.ArgumentNullException(nameof(blobsConfiguration));
            _exceptionHandler = exceptionHandler ?? throw new System.ArgumentNullException(nameof(exceptionHandler));
            _messageEnqueuedWatcherSetter = messageEnqueuedWatcherSetter ?? throw new System.ArgumentNullException(nameof(messageEnqueuedWatcherSetter));
            _blobWrittenWatcherSetter = blobWrittenWatcherSetter ?? throw new System.ArgumentNullException(nameof(blobWrittenWatcherSetter));
            _sharedContextProvider = sharedContextProvider ?? throw new System.ArgumentNullException(nameof(sharedContextProvider));
            _extensions = extensions ?? throw new System.ArgumentNullException(nameof(extensions));
            _singletonManager = singletonManager ?? throw new System.ArgumentNullException(nameof(singletonManager));
            _loggerFactory = loggerFactory ?? throw new System.ArgumentNullException(nameof(loggerFactory));
        }
        public ITriggerBindingProvider Create()
        {
            var innerProviders = new List<ITriggerBindingProvider>
            {
                new QueueTriggerAttributeBindingProvider(_nameResolver, _storageAccountProvider,
                _queueConfiguration.Value, _exceptionHandler, _messageEnqueuedWatcherSetter,
                _sharedContextProvider, _loggerFactory),

                new BlobTriggerAttributeBindingProvider(_nameResolver, _storageAccountProvider, _extensionTypeLocator,
                _hostIdProvider, _queueConfiguration.Value, _blobsConfiguration.Value, _exceptionHandler, _blobWrittenWatcherSetter,
                _messageEnqueuedWatcherSetter, _sharedContextProvider, _singletonManager, _loggerFactory)
            };

            // add any registered extension binding providers
            foreach (ITriggerBindingProvider provider in _extensions.GetExtensions(typeof(ITriggerBindingProvider)))
            {
                innerProviders.Add(provider);
            }

            return new CompositeTriggerBindingProvider(innerProviders);
        }
    }
}
