// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Converters;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.Config;
using System.Threading;
using System.Collections;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Bindings
{
#if false

    FunctionInstaceId !!! Converter doesn't have this... 


    - watcher? per config?  (same trick as Stream) 

    - Pass tests 

    nameResolver on connection string? 

    InvokeString , Log 

    customer types 


#endif

    internal class BlobExtension : IExtensionConfigProvider, 
        IAsyncConverter<BlobAttribute, Stream>,
        IAsyncConverter<BlobAttribute, CloudBlobStream>,
        IAsyncConverter<BlobAttribute, CloudBlockBlob>,
        IAsyncConverter<BlobAttribute, CloudPageBlob>,
        IAsyncConverter<BlobAttribute, CloudAppendBlob>,
        IAsyncConverter<BlobAttribute, ICloudBlob>,
        IAsyncConverter<BlobAttribute, CloudBlobContainer>,
        IAsyncConverter<BlobAttribute, CloudBlobDirectory>

    {
        private IStorageAccountProvider _accountProvider;
        private IContextGetter<IBlobWrittenWatcher> _blobWrittenWatcherGetter;


        #region Container rules
        async Task<CloudBlobContainer> IAsyncConverter<BlobAttribute, CloudBlobContainer>.ConvertAsync(
            BlobAttribute blobAttribute, CancellationToken cancellationToken)
        {
            var container = await GetContainerAsync(blobAttribute, cancellationToken);
            return container.SdkObject;
        }

        // Write-only rule. 
        async Task<CloudBlobDirectory> IAsyncConverter<BlobAttribute, CloudBlobDirectory>.ConvertAsync(
            BlobAttribute blobAttribute, CancellationToken cancellationToken)
        {
            IStorageBlobClient client = await GetClientAsync(blobAttribute, cancellationToken);

            BlobPath boundPath = BlobPath.ParseAndValidate(blobAttribute.BlobPath, isContainerBinding: false);

            IStorageBlobContainer container = client.GetContainerReference(boundPath.ContainerName);

            CloudBlobDirectory directory = container.SdkObject.GetDirectoryReference(
                boundPath.BlobName);

            return directory;
        }
        
        #endregion

        #region CloudBlob rules 

        // Write-only rule. 
        async Task<CloudBlobStream> IAsyncConverter<BlobAttribute, CloudBlobStream>.ConvertAsync(
            BlobAttribute blobAttribute, CancellationToken cancellationToken)
        {
            // $$$ Fix cast. 
            return (CloudBlobStream) await CreateStreamAsync(blobAttribute, cancellationToken);
        }


        async Task<CloudBlockBlob> IAsyncConverter<BlobAttribute, CloudBlockBlob>.ConvertAsync(
            BlobAttribute blobAttribute, CancellationToken cancellationToken)
        {
            var blob = await GetBlobAsync(blobAttribute, cancellationToken, typeof(CloudBlockBlob));
            return (CloudBlockBlob)(blob.SdkObject);
        }

        async Task<CloudPageBlob> IAsyncConverter<BlobAttribute, CloudPageBlob>.ConvertAsync(
    BlobAttribute blobAttribute, CancellationToken cancellationToken)
        {
            var blob = await GetBlobAsync(blobAttribute, cancellationToken, typeof(CloudPageBlob));
            return (CloudPageBlob)(blob.SdkObject);
        }

        async Task<CloudAppendBlob> IAsyncConverter<BlobAttribute, CloudAppendBlob>.ConvertAsync(
    BlobAttribute blobAttribute, CancellationToken cancellationToken)
        {
            var blob = await GetBlobAsync(blobAttribute, cancellationToken, typeof(CloudAppendBlob));
            return (CloudAppendBlob)(blob.SdkObject);
        }

        async Task<ICloudBlob> IAsyncConverter<BlobAttribute, ICloudBlob>.ConvertAsync(
    BlobAttribute blobAttribute, CancellationToken cancellationToken)
        {
            var blob = await GetBlobAsync(blobAttribute, cancellationToken, typeof(ICloudBlob));
            return (ICloudBlob)(blob.SdkObject);
        }
        #endregion 

        public async Task<Stream> ConvertAsync(BlobAttribute blobAttribute, CancellationToken cancellationToken)
        {
            return await CreateStreamAsync(blobAttribute, cancellationToken);
        }

        private async Task<Stream> CreateStreamAsync(BlobAttribute blobAttribute, CancellationToken cancellationToken)
        { 
            var fbc = new FunctionBindingContext(Guid.NewGuid(), cancellationToken, null);
            var vbc = new ValueBindingContext(fbc, cancellationToken);
            // $$$ Stamp with FunctionInstaceId, for causality 

            var blob = await GetBlobAsync(blobAttribute, cancellationToken);

            switch (blobAttribute.Access)
            {
                case FileAccess.Read:                    
                    var readStream = await ReadBlobArgumentBinding.TryBindStreamAsync(blob, vbc);
                    return readStream;                    

                case FileAccess.Write:                    
                    var writeStream = await WriteBlobArgumentBinding.BindStreamAsync(blob,
                    vbc, _blobWrittenWatcherGetter.Value);
                    return writeStream;

                default:
                    throw new InvalidOperationException("Cannot bind blob to Stream using FileAccess ReadWrite.");
            }
        }

        private async Task<IStorageBlobClient> GetClientAsync(
         BlobAttribute blobAttribute,
         CancellationToken cancellationToken)
        {
            IStorageAccount account = await _accountProvider.GetStorageAccountAsync(blobAttribute, cancellationToken);
            IStorageBlobClient client = account.CreateBlobClient();
            return client;
        }


        private async Task<IStorageBlobContainer> GetContainerAsync(
            BlobAttribute blobAttribute,
            CancellationToken cancellationToken)
        {
            IStorageBlobClient client = await GetClientAsync(blobAttribute, cancellationToken);

            BlobPath boundPath = BlobPath.ParseAndValidate(blobAttribute.BlobPath, isContainerBinding : true);

            IStorageBlobContainer container = client.GetContainerReference(boundPath.ContainerName);
            return container;
        }

            private async Task<IStorageBlob> GetBlobAsync(
            BlobAttribute blobAttribute, 
            CancellationToken cancellationToken,
            Type requestedType = null)
        {
            IStorageBlobClient client = await GetClientAsync(blobAttribute, cancellationToken);

            // $$$ This handles URL; but we could make it fully handle any SAS  URL, and skip Connection string. 
            BlobPath boundPath = BlobPath.ParseAndValidate(blobAttribute.BlobPath);

            IStorageBlobContainer container = client.GetContainerReference(boundPath.ContainerName);

            if (blobAttribute.Access != FileAccess.Read)
            {
                await container.CreateIfNotExistsAsync(cancellationToken);
            }

            IStorageBlob blob = await container.GetBlobReferenceForArgumentTypeAsync(
                boundPath.BlobName, requestedType, cancellationToken);

            return blob;
        }


        class XXXBinder<T> : IAsyncConverter<CloudBlobContainer, IEnumerable<T>>
        {
            public XXXBinder(BlobExtension parent)
            {
            }
            public async Task<IEnumerable<T>> ConvertAsync(CloudBlobContainer container, CancellationToken cancellationToken)
            {
                /*
                // Query the blob container using the blob prefix (if specified)
                // Note that we're explicitly using useFlatBlobListing=true to collapse
                // sub directories. If users want to bind to a sub directory, they can
                // bind to CloudBlobDirectory.
                string prefix = containerBindingContext.Path.BlobName;
                IEnumerable<IStorageListBlobItem> blobItems = await container.ListBlobsAsync(
                    prefix, true, cancellationToken);

    */

                throw new NotImplementedException("Convert to " + typeof(T));
            }
        }

        public void Initialize(ExtensionConfigContext context)
        {
            _accountProvider = context.Config.GetService<IStorageAccountProvider>();

            // $$$ Per-host 
            _blobWrittenWatcherGetter = context.PerHostServices.GetService<ContextAccessor<IBlobWrittenWatcher>>();
            

            

            var rule = context.AddBindingRule<BlobAttribute>();


            rule.BindToInput<CloudBlobContainer>(this);
            rule.BindToInput<CloudBlobDirectory>(this);

            //rule.BindToInput<IEnumerable<OpenType>>(typeof(XXXBinder<>), this);

            rule.AddConverter<CloudBlobContainer, IEnumerable<OpenType>>(typeof(XXXBinder<>), this);

            // CloudBlobEnumerableArgumentBindingProvider
#if false
                        if (parameter.ParameterType == typeof(IEnumerable<ICloudBlob>) ||
                parameter.ParameterType == typeof(IEnumerable<CloudBlockBlob>) ||
                parameter.ParameterType == typeof(IEnumerable<CloudPageBlob>) ||
                parameter.ParameterType == typeof(IEnumerable<CloudAppendBlob>) ||

            // All converted from ICloudBlob 
                parameter.ParameterType == typeof(IEnumerable<TextReader>) ||
                parameter.ParameterType == typeof(IEnumerable<Stream>) ||
                parameter.ParameterType == typeof(IEnumerable<string>))
#endif

            rule.BindToStream(this, FileAccess.ReadWrite); // Precedence, must beat CloudBlobStream

            // Normal blob
            rule.BindToInput<CloudBlockBlob>(this);
            rule.BindToInput<CloudPageBlob>(this);
            rule.BindToInput<CloudAppendBlob>(this);
            rule.BindToInput<ICloudBlob>(this); // base interface 

            // $$$ Only when Access == FileAccess.Write
            rule.BindToInput<CloudBlobStream>(this);            

            

            // $$$ Custom types via a Converter  (replace ICloudStreamBinder) 
        }
    }

    internal class BlobAttributeBindingProvider : IBindingProvider, IBindingRuleProvider
    {
        private readonly INameResolver _nameResolver;
        private readonly IStorageAccountProvider _accountProvider;
        private readonly IBlobArgumentBindingProvider _blobArgumentProvider;
        private readonly IBlobContainerArgumentBindingProvider _blobContainerArgumentProvider;

        public BlobAttributeBindingProvider(INameResolver nameResolver, IStorageAccountProvider accountProvider,
            IExtensionTypeLocator extensionTypeLocator, IContextGetter<IBlobWrittenWatcher> blobWrittenWatcherGetter)
        {
            if (accountProvider == null)
            {
                throw new ArgumentNullException("accountProvider");
            }

            if (extensionTypeLocator == null)
            {
                throw new ArgumentNullException("extensionTypeLocator");
            }

            if (blobWrittenWatcherGetter == null)
            {
                throw new ArgumentNullException("blobWrittenWatcherGetter");
            }

            _nameResolver = nameResolver;
            _accountProvider = accountProvider;
            _blobArgumentProvider = CreateBlobArgumentProvider(extensionTypeLocator.GetCloudBlobStreamBinderTypes(), blobWrittenWatcherGetter);
            _blobContainerArgumentProvider = CreateBlobContainerArgumentProvider();
        }

        public async Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            ParameterInfo parameter = context.Parameter;
            var blobAttribute = TypeUtility.GetResolvedAttribute<BlobAttribute>(parameter);

            if (blobAttribute == null)
            {
                return null;
            }

            string resolvedPath = Resolve(blobAttribute.BlobPath);
            IBindableBlobPath path = null;
            IStorageAccount account = await _accountProvider.GetStorageAccountAsync(blobAttribute, context.CancellationToken, _nameResolver);
            StorageClientFactoryContext clientFactoryContext = new StorageClientFactoryContext
            {
                Parameter = context.Parameter
            };
            IStorageBlobClient client = account.CreateBlobClient(clientFactoryContext);

            // first try to bind to the Container
            IArgumentBinding<IStorageBlobContainer> containerArgumentBinding = _blobContainerArgumentProvider.TryCreate(parameter);
            if (containerArgumentBinding == null)
            {
                // if this isn't a Container binding, try a Blob binding
                IBlobArgumentBinding blobArgumentBinding = _blobArgumentProvider.TryCreate(parameter, blobAttribute.Access);
                if (blobArgumentBinding == null)
                {
                    throw new InvalidOperationException("Can't bind Blob to type '" + parameter.ParameterType + "'.");
                }

                path = BindableBlobPath.Create(resolvedPath);
                path.ValidateContractCompatibility(context.BindingDataContract);

                return new BlobBinding(parameter.Name, blobArgumentBinding, client, path);
            }

            path = BindableBlobPath.Create(resolvedPath, isContainerBinding: true);
            path.ValidateContractCompatibility(context.BindingDataContract);
            BlobContainerBinding.ValidateContainerBinding(blobAttribute, parameter.ParameterType, path);

            return new BlobContainerBinding(parameter.Name, containerArgumentBinding, client, path);
        }

        private string Resolve(string blobName)
        {
            if (_nameResolver == null)
            {
                return blobName;
            }

            return _nameResolver.ResolveWholeString(blobName);
        }

        private static IBlobArgumentBindingProvider CreateBlobArgumentProvider(IEnumerable<Type> cloudBlobStreamBinderTypes,
            IContextGetter<IBlobWrittenWatcher> blobWrittenWatcherGetter)
        {
            List<IBlobArgumentBindingProvider> innerProviders = new List<IBlobArgumentBindingProvider>();

            innerProviders.Add(CreateConverterProvider<ICloudBlob, StorageBlobToCloudBlobConverter>());
            innerProviders.Add(CreateConverterProvider<CloudBlockBlob, StorageBlobToCloudBlockBlobConverter>());
            innerProviders.Add(CreateConverterProvider<CloudPageBlob, StorageBlobToCloudPageBlobConverter>());
            innerProviders.Add(CreateConverterProvider<CloudAppendBlob, StorageBlobToCloudAppendBlobConverter>());
            innerProviders.Add(new StreamArgumentBindingProvider(blobWrittenWatcherGetter));
            innerProviders.Add(new CloudBlobStreamArgumentBindingProvider(blobWrittenWatcherGetter));
            innerProviders.Add(new TextReaderArgumentBindingProvider());
            innerProviders.Add(new TextWriterArgumentBindingProvider(blobWrittenWatcherGetter));
            innerProviders.Add(new StringArgumentBindingProvider());
            innerProviders.Add(new ByteArrayArgumentBindingProvider());
            innerProviders.Add(new OutStringArgumentBindingProvider(blobWrittenWatcherGetter));
            innerProviders.Add(new OutByteArrayArgumentBindingProvider(blobWrittenWatcherGetter));

            if (cloudBlobStreamBinderTypes != null)
            {
                innerProviders.AddRange(cloudBlobStreamBinderTypes.Select(
                    t => CloudBlobStreamObjectBinder.CreateReadBindingProvider(t)));

                innerProviders.AddRange(cloudBlobStreamBinderTypes.Select(
                    t => CloudBlobStreamObjectBinder.CreateWriteBindingProvider(t, blobWrittenWatcherGetter)));
            }

            return new CompositeBlobArgumentBindingProvider(innerProviders);
        }

        private static IBlobContainerArgumentBindingProvider CreateBlobContainerArgumentProvider()
        {
            List<IBlobContainerArgumentBindingProvider> innerProviders = new List<IBlobContainerArgumentBindingProvider>();

            innerProviders.Add(new CloudBlobContainerArgumentBindingProvider());
            innerProviders.Add(new CloudBlobDirectoryArgumentBindingProvider());
            innerProviders.Add(new CloudBlobEnumerableArgumentBindingProvider());

            return new CompositeBlobContainerArgumentBindingProvider(innerProviders);
        }

        private static IBlobArgumentBindingProvider CreateConverterProvider<TValue, TConverter>()
            where TConverter : IConverter<IStorageBlob, TValue>, new()
        {
            return new ConverterArgumentBindingProvider<TValue>(new TConverter());
        }
        public IEnumerable<BindingRule> GetRules()
        {
            // Once we have a BindToStream rule, we shouldn't need this. 
            // https://github.com/Azure/azure-webjobs-sdk/issues/1001 
            foreach (var type in new Type[]
            {
                typeof(Stream),
                typeof(TextReader),
                typeof(TextWriter),
                typeof(ICloudBlob),
                typeof(CloudBlockBlob),
                typeof(CloudPageBlob),
                typeof(CloudAppendBlob),
                typeof(string),
                typeof(byte[]),
                typeof(string).MakeByRefType(),
                typeof(byte[]).MakeByRefType()
            })
            {
                yield return new BindingRule
                {
                    SourceAttribute = typeof(BlobAttribute),
                    UserType = new ConverterManager.ExactMatch(type)
                };
            }
        }

        public Type GetDefaultType(Attribute attribute, FileAccess access, Type requestedType)
        {
            if (attribute is BlobAttribute)
            {
                return typeof(Stream);
            }
            return null;
        }
    }
}