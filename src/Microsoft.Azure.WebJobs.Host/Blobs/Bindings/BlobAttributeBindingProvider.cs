﻿// Copyright (c) .NET Foundation. All rights reserved.
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

namespace Microsoft.Azure.WebJobs.Host.Blobs.Bindings
{
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
                    UserType = OpenType.FromType(type)
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