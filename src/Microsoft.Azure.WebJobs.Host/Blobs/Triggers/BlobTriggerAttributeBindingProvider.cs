// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.Config;
using System.Threading;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Triggers
{
    internal class BlobTriggerExtensionConfig : IExtensionConfigProvider        
    {
        private IStorageAccountProvider _accountProvider;

        public void Initialize(ExtensionConfigContext context)
        {
            _accountProvider = context.Config.GetService<IStorageAccountProvider>();

            var rule = context.AddBindingRule<BlobTriggerAttribute>();
            rule.BindToTrigger<IStorageBlob>();

            rule.AddConverter<IStorageBlob, DirectInvokeString>(blob => new DirectInvokeString(blob.GetBlobPath()));
            rule.AddConverter<DirectInvokeString, IStorageBlob>(ConvertFromInvokeString);

            // Common converters shared between [Blob] and [BlobTrigger]

            // Trigger already has the IStorageBlob. Whereas BindToInput defines: Attr-->Stream. 
            //  Converter manager already has Stream-->Byte[],String,TextReader
            context.AddConverter<IStorageBlob, Stream>(ConvertToStreamAsync);

            // Blob type is a property of an existing blob. 
            context.AddConverter(new StorageBlobToCloudBlobConverter());
            context.AddConverter(new StorageBlobToCloudBlockBlobConverter());
            context.AddConverter(new StorageBlobToCloudPageBlobConverter());
            context.AddConverter(new StorageBlobToCloudAppendBlobConverter());
        }

        private async Task<Stream> ConvertToStreamAsync(IStorageBlob input, CancellationToken cancellationToken)
        {
            WatchableReadStream watchableStream = await ReadBlobArgumentBinding.TryBindStreamAsync(input, cancellationToken);
            return watchableStream;
        }

        // For describing InvokeStrings.
        private async Task<IStorageBlob> ConvertFromInvokeString(DirectInvokeString input, Attribute attr, ValueBindingContext context)
        {
            var attrResolved = (BlobTriggerAttribute)attr;
            var account = await _accountProvider.GetStorageAccountAsync(attrResolved, CancellationToken.None);
            var client = account.CreateBlobClient();

            var cancellationToken = context.CancellationToken;
            BlobPath path = BlobPath.ParseAndValidate(input.Value);
            IStorageBlobContainer container = client.GetContainerReference(path.ContainerName);
            var blob = await container.GetBlobReferenceFromServerAsync(path.BlobName, cancellationToken);

            return blob;
        }
    }

    internal class BlobTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly INameResolver _nameResolver;
        private readonly IStorageAccountProvider _accountProvider;
        private readonly IHostIdProvider _hostIdProvider;
        private readonly IQueueConfiguration _queueConfiguration;
        private readonly JobHostBlobsConfiguration _blobsConfiguration;
        private readonly IWebJobsExceptionHandler _exceptionHandler;
        private readonly IContextSetter<IBlobWrittenWatcher> _blobWrittenWatcherSetter;
        private readonly IContextSetter<IMessageEnqueuedWatcher> _messageEnqueuedWatcherSetter;
        private readonly ISharedContextProvider _sharedContextProvider;
        private readonly SingletonManager _singletonManager;
        private readonly TraceWriter _trace;
        private readonly ILoggerFactory _loggerFactory;

        public BlobTriggerAttributeBindingProvider(INameResolver nameResolver,
            IStorageAccountProvider accountProvider,
            IExtensionTypeLocator extensionTypeLocator,
            IHostIdProvider hostIdProvider,
            IQueueConfiguration queueConfiguration,
            JobHostBlobsConfiguration blobsConfiguration,
            IWebJobsExceptionHandler exceptionHandler,
            IContextSetter<IBlobWrittenWatcher> blobWrittenWatcherSetter,
            IContextSetter<IMessageEnqueuedWatcher> messageEnqueuedWatcherSetter,
            ISharedContextProvider sharedContextProvider,
            SingletonManager singletonManager,
            TraceWriter trace,
            ILoggerFactory loggerFactory)
        {
            if (accountProvider == null)
            {
                throw new ArgumentNullException("accountProvider");
            }

            if (extensionTypeLocator == null)
            {
                throw new ArgumentNullException("extensionTypeLocator");
            }

            if (hostIdProvider == null)
            {
                throw new ArgumentNullException("hostIdProvider");
            }

            if (queueConfiguration == null)
            {
                throw new ArgumentNullException("queueConfiguration");
            }

            if (blobsConfiguration == null)
            {
                throw new ArgumentNullException("blobsConfiguration");
            }

            if (exceptionHandler == null)
            {
                throw new ArgumentNullException("exceptionHandler");
            }

            if (blobWrittenWatcherSetter == null)
            {
                throw new ArgumentNullException("blobWrittenWatcherSetter");
            }

            if (messageEnqueuedWatcherSetter == null)
            {
                throw new ArgumentNullException("messageEnqueuedWatcherSetter");
            }

            if (sharedContextProvider == null)
            {
                throw new ArgumentNullException("sharedContextProvider");
            }

            if (singletonManager == null)
            {
                throw new ArgumentNullException("singletonManager");
            }

            if (trace == null)
            {
                throw new ArgumentNullException("trace");
            }

            _nameResolver = nameResolver;
            _accountProvider = accountProvider;
            _hostIdProvider = hostIdProvider;
            _queueConfiguration = queueConfiguration;
            _blobsConfiguration = blobsConfiguration;
            _exceptionHandler = exceptionHandler;
            _blobWrittenWatcherSetter = blobWrittenWatcherSetter;
            _messageEnqueuedWatcherSetter = messageEnqueuedWatcherSetter;
            _sharedContextProvider = sharedContextProvider;
            _singletonManager = singletonManager;
            _trace = trace;
            _loggerFactory = loggerFactory;
        }

        public async Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            ParameterInfo parameter = context.Parameter;
            var blobTriggerAttribute = TypeUtility.GetResolvedAttribute<BlobTriggerAttribute>(context.Parameter);

            if (blobTriggerAttribute == null)
            {
                return null;
            }

            string resolvedCombinedPath = Resolve(blobTriggerAttribute.BlobPath);
            IBlobPathSource path = BlobPathSource.Create(resolvedCombinedPath);

            IStorageAccount hostAccount = await _accountProvider.GetStorageAccountAsync(context.CancellationToken);
            IStorageAccount dataAccount = await _accountProvider.GetStorageAccountAsync(blobTriggerAttribute, context.CancellationToken, _nameResolver);
            // premium does not support blob logs, so disallow for blob triggers
            dataAccount.AssertTypeOneOf(StorageAccountType.GeneralPurpose, StorageAccountType.BlobOnly);

            ITriggerBinding binding = new BlobTriggerBinding(parameter, hostAccount, dataAccount, path,
                _hostIdProvider, _queueConfiguration, _blobsConfiguration, _exceptionHandler, _blobWrittenWatcherSetter,
                _messageEnqueuedWatcherSetter, _sharedContextProvider, _singletonManager, _trace, _loggerFactory);

            return binding;
        }

        private string Resolve(string queueName)
        {
            if (_nameResolver == null)
            {
                return queueName;
            }

            return _nameResolver.ResolveWholeString(queueName);
        }
    }
}
