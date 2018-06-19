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
using Microsoft.Azure.WebJobs.Host.Queues.Listeners;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Triggers
{
    internal class BlobTriggerExtensionConfig : IExtensionConfigProvider
    {
        private StorageAccountProvider _accountProvider;
        private BlobTriggerAttributeBindingProvider _triggerBinder;

        public BlobTriggerExtensionConfig(StorageAccountProvider accountProvider, BlobTriggerAttributeBindingProvider triggerBinder)
        {
            _accountProvider = accountProvider;
            _triggerBinder = triggerBinder;
        }

        public void Initialize(ExtensionConfigContext context)
        {
            var rule = context.AddBindingRule<BlobTriggerAttribute>();
            rule.BindToTrigger<ICloudBlob>(_triggerBinder);

            rule.AddConverter<ICloudBlob, DirectInvokeString>(blob => new DirectInvokeString(blob.GetBlobPath()));
            rule.AddConverter<DirectInvokeString, ICloudBlob>(ConvertFromInvokeString);

            // Common converters shared between [Blob] and [BlobTrigger]

            // Trigger already has the IStorageBlob. Whereas BindToInput defines: Attr-->Stream. 
            //  Converter manager already has Stream-->Byte[],String,TextReader
            context.AddConverter<ICloudBlob, Stream>(ConvertToStreamAsync);

            // Blob type is a property of an existing blob.             
            // $$$ did we lose CloudBlob. That's a base class for Cloud*Blob, but does not implement ICloudBlob? 
            context.AddConverter(new StorageBlobConverter<CloudAppendBlob>());
            context.AddConverter(new StorageBlobConverter<CloudBlockBlob>());
            context.AddConverter(new StorageBlobConverter<CloudPageBlob>());
        }

        private async Task<Stream> ConvertToStreamAsync(ICloudBlob input, CancellationToken cancellationToken)
        {
            WatchableReadStream watchableStream = await ReadBlobArgumentBinding.TryBindStreamAsync(input, cancellationToken);
            return watchableStream;
        }

        // For describing InvokeStrings.
        private async Task<ICloudBlob> ConvertFromInvokeString(DirectInvokeString input, Attribute attr, ValueBindingContext context)
        {
            var attrResolved = (BlobTriggerAttribute)attr;

            var account = _accountProvider.Get(attrResolved.Connection);
            var client = account.CreateCloudBlobClient();
            BlobPath path = BlobPath.ParseAndValidate(input.Value);
            var container = client.GetContainerReference(path.ContainerName);
            var blob = await container.GetBlobReferenceFromServerAsync(path.BlobName);

            return blob;
        }
    }

    internal class BlobTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly INameResolver _nameResolver;
        private readonly StorageAccountProvider _accountProvider;
        private readonly IHostIdProvider _hostIdProvider;
        private readonly JobHostQueuesOptions _queueOptions;
        private readonly JobHostBlobsOptions _blobsOptions;
        private readonly IWebJobsExceptionHandler _exceptionHandler;
        private readonly IContextSetter<IBlobWrittenWatcher> _blobWrittenWatcherSetter;
        private readonly SharedQueueWatcher _messageEnqueuedWatcherSetter;
        private readonly ISharedContextProvider _sharedContextProvider;
        private readonly IHostSingletonManager _singletonManager;
        private readonly ILoggerFactory _loggerFactory;

        public BlobTriggerAttributeBindingProvider(INameResolver nameResolver,

            StorageAccountProvider accountProvider,
            IHostIdProvider hostIdProvider,
            IOptions<JobHostQueuesOptions> queueOptions,
            IOptions<JobHostBlobsOptions> blobsConfiguration,
            IWebJobsExceptionHandler exceptionHandler,
            IContextSetter<IBlobWrittenWatcher> blobWrittenWatcherSetter,
            SharedQueueWatcher messageEnqueuedWatcherSetter,
            ISharedContextProvider sharedContextProvider,
            IHostSingletonManager singletonManager,
            ILoggerFactory loggerFactory)
        {
            _accountProvider = accountProvider ?? throw new ArgumentNullException(nameof(accountProvider));
            _hostIdProvider = hostIdProvider ?? throw new ArgumentNullException(nameof(hostIdProvider));
            _queueOptions = (queueOptions ?? throw new ArgumentNullException(nameof(queueOptions))).Value;
            _blobsOptions = (blobsConfiguration?? throw new ArgumentNullException(nameof(blobsConfiguration))).Value;
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

            var hostAccount = _accountProvider.GetHost();
            var dataAccount = _accountProvider.Get(blobTriggerAttribute.Connection, _nameResolver);

            // premium does not support blob logs, so disallow for blob triggers
            // dataAccount.AssertTypeOneOf(StorageAccountType.GeneralPurpose, StorageAccountType.BlobOnly); $$$

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
