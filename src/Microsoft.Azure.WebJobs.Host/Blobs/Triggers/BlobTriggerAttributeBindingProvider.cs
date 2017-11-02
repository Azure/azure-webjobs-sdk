// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Triggers
{
    internal class BlobTriggerExtensionConfig : IExtensionConfigProvider
    {
        private IStorageAccountProvider _accountProvider;

        public BlobTriggerExtensionConfig(IStorageAccountProvider accountProvider)
        {
            _accountProvider = accountProvider;
        }

        public void Initialize(ExtensionConfigContext context)
        {
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
        private readonly JobHostQueuesOptions _queueOptions;
        private readonly JobHostBlobsOptions _blobsOptions;
        private readonly IWebJobsExceptionHandler _exceptionHandler;
        private readonly IContextSetter<IBlobWrittenWatcher> _blobWrittenWatcherSetter;
        private readonly IContextSetter<IMessageEnqueuedWatcher> _messageEnqueuedWatcherSetter;
        private readonly ISharedContextProvider _sharedContextProvider;
        private readonly SingletonManager _singletonManager;
        private readonly ILoggerFactory _loggerFactory;

        public BlobTriggerAttributeBindingProvider(INameResolver nameResolver,
            IStorageAccountProvider accountProvider,
            IExtensionTypeLocator extensionTypeLocator,
            IHostIdProvider hostIdProvider,
            JobHostQueuesOptions queueOptions,
            JobHostBlobsOptions blobsConfiguration,
            IWebJobsExceptionHandler exceptionHandler,
            IContextSetter<IBlobWrittenWatcher> blobWrittenWatcherSetter,
            IContextSetter<IMessageEnqueuedWatcher> messageEnqueuedWatcherSetter,
            ISharedContextProvider sharedContextProvider,
            SingletonManager singletonManager,
            ILoggerFactory loggerFactory)
        {
            if (extensionTypeLocator == null)
            {
                throw new ArgumentNullException(nameof(extensionTypeLocator));
            }

            _accountProvider = accountProvider ?? throw new ArgumentNullException(nameof(accountProvider));
            _hostIdProvider = hostIdProvider ?? throw new ArgumentNullException(nameof(hostIdProvider));
            _queueOptions = queueOptions ?? throw new ArgumentNullException(nameof(queueOptions));
            _blobsOptions = blobsConfiguration ?? throw new ArgumentNullException(nameof(blobsConfiguration));
            _exceptionHandler = exceptionHandler ?? throw new ArgumentNullException(nameof(exceptionHandler));
            _blobWrittenWatcherSetter = blobWrittenWatcherSetter ?? throw new ArgumentNullException(nameof(blobWrittenWatcherSetter));
            _messageEnqueuedWatcherSetter = messageEnqueuedWatcherSetter ?? throw new ArgumentNullException(nameof(messageEnqueuedWatcherSetter));
            _sharedContextProvider = sharedContextProvider ?? throw new ArgumentNullException(nameof(sharedContextProvider));
            _singletonManager = singletonManager ?? throw new ArgumentNullException(nameof(singletonManager));

            _nameResolver = nameResolver;
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
                _hostIdProvider, _queueOptions, _blobsOptions, _exceptionHandler, _blobWrittenWatcherSetter,
                _messageEnqueuedWatcherSetter, _sharedContextProvider, _singletonManager, _loggerFactory);

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
