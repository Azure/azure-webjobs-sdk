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
    internal class BlobTriggerExtension : IExtensionConfigProvider,
        IAsyncConverter<string, IStorageBlob>, // for invoker
        IAsyncConverter<IStorageBlob, Stream>,
         IAsyncConverter<Stream, string>,
        IAsyncConverter<Stream, byte[]>,
        IAsyncConverter<Stream, TextReader>
    {
        public void Initialize(ExtensionConfigContext context)
        {
            var rule = context.AddBindingRule<BlobTriggerAttribute>();
            rule.BindToTrigger<IStorageBlob>(); // Add listener here too? $$$ Condense with AddBindingRule

            // We can now reuse converters from BindToInput/BindToStream
            // $$$ Reduce BindToStream to a converter problem. 
            rule.AddConverter<string, IStorageBlob>(this); // for direct invoker
            rule.AddConverter<IStorageBlob, Stream>(this);

            rule.AddConverter(new StorageBlobToCloudBlobConverter());
            rule.AddConverter(new StorageBlobToCloudBlockBlobConverter());
            rule.AddConverter(new StorageBlobToCloudPageBlobConverter());
            rule.AddConverter(new StorageBlobToCloudAppendBlobConverter());

            rule.AddConverter<Stream, string>(this);
            rule.AddConverter<Stream, byte[]>(this);
            rule.AddConverter<Stream, TextReader>(this);

            // ApplyRules() will have _binders == 0.
            // Add a Binding to Non-trigger _BindingProvider  for this ; which FunctionIndexer will claim. 
            // Don't event add a BindingProvider ... that can get confusing. 
            // !!! Just search for converters! If converter exist, then create the rule. 

            // Also check explicit invoke ... Obj is a string ... (can we convert string to IStorageBlob )
        }

        // StringArgumentBindingProvider
        async Task<string> IAsyncConverter<Stream, string>.ConvertAsync(Stream input, CancellationToken cancellationToken)
        {
            string value;

            using (input)
            using (TextReader reader = new StreamReader(input))
            {
                cancellationToken.ThrowIfCancellationRequested();
                value = await reader.ReadToEndAsync();
                return value;
            }
        }

        // ByteArrayArgumentBindingProvider
        async Task<byte[]> IAsyncConverter<Stream, byte[]>.ConvertAsync(Stream input, CancellationToken cancellationToken)
        {
            byte[] value;

            using (input)
            using (MemoryStream outputStream = new MemoryStream())
            {
                const int DefaultBufferSize = 4096;
                await input.CopyToAsync(outputStream, DefaultBufferSize);
                value = outputStream.ToArray();
                return value;
            }
        }

        // TextReaderArgumentBindingProvider
        async Task<TextReader> IAsyncConverter<Stream, TextReader>.ConvertAsync(Stream input, CancellationToken cancellationToken)
        {
            return new StreamReader(input);
        }


        async Task<Stream> IAsyncConverter<IStorageBlob, Stream>.ConvertAsync(IStorageBlob input, CancellationToken cancellationToken)
        {
            WatchableReadStream watchableStream = await ReadBlobArgumentBinding.TryBindStreamAsync(input, cancellationToken);
            return watchableStream;
        }

        internal static StringToStorageBlobConverter _invoker;

        Task<IStorageBlob> IAsyncConverter<string, IStorageBlob>.ConvertAsync(string input, CancellationToken cancellationToken)
        {
            return _invoker.ConvertAsync(input, cancellationToken);
        }
    }



    internal class BlobTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly INameResolver _nameResolver;
        private readonly IStorageAccountProvider _accountProvider;
        private readonly IBlobArgumentBindingProvider _provider;
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
            _provider = CreateProvider(extensionTypeLocator.GetCloudBlobStreamBinderTypes());
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

        private static IBlobArgumentBindingProvider CreateProvider(IEnumerable<Type> cloudBlobStreamBinderTypes)
        {
            List<IBlobArgumentBindingProvider> innerProviders = new List<IBlobArgumentBindingProvider>();

            innerProviders.Add(CreateConverterProvider<ICloudBlob, StorageBlobToCloudBlobConverter>());
            innerProviders.Add(CreateConverterProvider<CloudBlockBlob, StorageBlobToCloudBlockBlobConverter>());
            innerProviders.Add(CreateConverterProvider<CloudPageBlob, StorageBlobToCloudPageBlobConverter>());
            innerProviders.Add(CreateConverterProvider<CloudAppendBlob, StorageBlobToCloudAppendBlobConverter>());
            innerProviders.Add(new StreamArgumentBindingProvider(defaultAccess: FileAccess.Read));
            innerProviders.Add(new TextReaderArgumentBindingProvider());
            innerProviders.Add(new StringArgumentBindingProvider());
            innerProviders.Add(new ByteArrayArgumentBindingProvider());

            if (cloudBlobStreamBinderTypes != null)
            {
                innerProviders.AddRange(cloudBlobStreamBinderTypes.Select(
                    t => CloudBlobStreamObjectBinder.CreateReadBindingProvider(t)));
            }

            return new CompositeBlobArgumentBindingProvider(innerProviders);
        }

        private static IBlobArgumentBindingProvider CreateConverterProvider<TValue, TConverter>()
            where TConverter : IConverter<IStorageBlob, TValue>, new()
        {
            return new ConverterArgumentBindingProvider<TValue>(new TConverter());
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

            IArgumentBinding<IStorageBlob> argumentBinding = _provider.TryCreate(parameter, access: null);
            if (argumentBinding == null)
            {
                throw new InvalidOperationException("Can't bind BlobTrigger to type '" + parameter.ParameterType + "'.");
            }

            IStorageAccount hostAccount = await _accountProvider.GetStorageAccountAsync(context.CancellationToken);
            IStorageAccount dataAccount = await _accountProvider.GetStorageAccountAsync(blobTriggerAttribute, context.CancellationToken, _nameResolver);
            // premium does not support blob logs, so disallow for blob triggers
            dataAccount.AssertTypeOneOf(StorageAccountType.GeneralPurpose, StorageAccountType.BlobOnly);

            ITriggerBinding binding = new BlobTriggerBinding(parameter, argumentBinding, hostAccount, dataAccount, path,
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
