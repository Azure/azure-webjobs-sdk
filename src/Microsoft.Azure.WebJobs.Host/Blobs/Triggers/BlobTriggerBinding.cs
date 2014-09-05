﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Blobs.Bindings;
using Microsoft.Azure.WebJobs.Host.Blobs.Listeners;
using Microsoft.Azure.WebJobs.Host.Converters;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Triggers
{
    internal class BlobTriggerBinding : ITriggerBinding<ICloudBlob>
    {
        private readonly string _parameterName;
        private readonly IArgumentBinding<ICloudBlob> _argumentBinding;
        private readonly CloudBlobClient _client;
        private readonly string _accountName;
        private readonly IBlobPathSource _path;
        private readonly IAsyncObjectToTypeConverter<ICloudBlob> _converter;

        public BlobTriggerBinding(string parameterName, IArgumentBinding<ICloudBlob> argumentBinding,
            CloudBlobClient client, IBlobPathSource path)
        {
            _parameterName = parameterName;
            _argumentBinding = argumentBinding;
            _client = client;
            _accountName = BlobClient.GetAccountName(client);
            _path = path;
            _converter = CreateConverter(client);
        }

        public IReadOnlyDictionary<string, Type> BindingDataContract
        {
            get { return _path.CreateBindingDataContract(); }
        }

        public string ContainerName
        {
            get { return _path.ContainerNamePattern; }
        }

        public string BlobName
        {
            get { return _path.BlobNamePattern; }
        }

        public string BlobPath
        {
            get { return _path.ToString(); }
        }

        private FileAccess Access
        {
            get
            {
                return typeof(ICloudBlob).IsAssignableFrom(_argumentBinding.ValueType)
                    ? FileAccess.ReadWrite : FileAccess.Read;
            }
        }

        private static IAsyncObjectToTypeConverter<ICloudBlob> CreateConverter(CloudBlobClient client)
        {
            return new CompositeAsyncObjectToTypeConverter<ICloudBlob>(
                new OutputConverter<ICloudBlob>(new AsyncIdentityConverter<ICloudBlob>()),
                new OutputConverter<string>(new StringToCloudBlobConverter(client)));
        }

        public async Task<ITriggerData> BindAsync(ICloudBlob value, ValueBindingContext context)
        {
            IValueProvider valueProvider = await _argumentBinding.BindAsync(value, context);
            IReadOnlyDictionary<string, object> bindingData = CreateBindingData(value);

            return new TriggerData(valueProvider, bindingData);
        }

        public async Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            ConversionResult<ICloudBlob> conversionResult = await _converter.TryConvertAsync(value,
                context.CancellationToken);

            if (!conversionResult.Succeeded)
            {
                throw new InvalidOperationException("Unable to convert trigger to ICloudBlob.");
            }

            return await BindAsync(conversionResult.Result, context);
        }

        public IFunctionDefinition CreateFunctionDefinition(IReadOnlyDictionary<string, IBinding> nonTriggerBindings,
            IInvoker invoker, FunctionDescriptor functionDescriptor, MethodInfo method)
        {
            ITriggeredFunctionBinding<ICloudBlob> functionBinding =
                new TriggeredFunctionBinding<ICloudBlob>(_parameterName, this, nonTriggerBindings);
            ITriggeredFunctionInstanceFactory<ICloudBlob> instanceFactory =
                new TriggeredFunctionInstanceFactory<ICloudBlob>(functionBinding, invoker, functionDescriptor, method);
            CloudBlobContainer container = _client.GetContainerReference(_path.ContainerNamePattern);
            IListenerFactory listenerFactory = new BlobListenerFactory(functionDescriptor.Id, container, _path,
                instanceFactory);
            return new FunctionDefinition(instanceFactory, listenerFactory);
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new BlobTriggerParameterDescriptor
            {
                Name = _parameterName,
                AccountName = _accountName,
                ContainerName = _path.ContainerNamePattern,
                BlobName = _path.BlobNamePattern,
                Access = Access
            };
        }

        private IReadOnlyDictionary<string, object> CreateBindingData(ICloudBlob value)
        {
            return _path.CreateBindingData(value.ToBlobPath());
        }
    }
}
