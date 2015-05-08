﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Blobs.Listeners;
using Microsoft.Azure.WebJobs.Host.Converters;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Triggers
{
    internal class BlobTriggerBinding : ITriggerBinding<IStorageBlob>
    {
        private readonly string _parameterName;
        private readonly IArgumentBinding<IStorageBlob> _argumentBinding;
        private readonly IStorageAccount _account;
        private readonly IStorageBlobClient _client;
        private readonly string _accountName;
        private readonly IBlobPathSource _path;
        private readonly IHostIdProvider _hostIdProvider;
        private readonly IQueueConfiguration _queueConfiguration;
        private readonly IBackgroundExceptionDispatcher _backgroundExceptionDispatcher;
        private readonly IContextSetter<IBlobWrittenWatcher> _blobWrittenWatcherSetter;
        private readonly IContextSetter<IMessageEnqueuedWatcher> _messageEnqueuedWatcherSetter;
        private readonly ISharedContextProvider _sharedContextProvider;
        private readonly TextWriter _log;
        private readonly IAsyncObjectToTypeConverter<IStorageBlob> _converter;
        private readonly IReadOnlyDictionary<string, Type> _bindingDataContract;

        public BlobTriggerBinding(string parameterName,
            IArgumentBinding<IStorageBlob> argumentBinding,
            IStorageAccount account,
            IBlobPathSource path,
            IHostIdProvider hostIdProvider,
            IQueueConfiguration queueConfiguration,
            IBackgroundExceptionDispatcher backgroundExceptionDispatcher,
            IContextSetter<IBlobWrittenWatcher> blobWrittenWatcherSetter,
            IContextSetter<IMessageEnqueuedWatcher> messageEnqueuedWatcherSetter,
            ISharedContextProvider sharedContextProvider,
            TextWriter log)
        {
            if (argumentBinding == null)
            {
                throw new ArgumentNullException("argumentBinding");
            }

            if (account == null)
            {
                throw new ArgumentNullException("account");
            }

            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            if (hostIdProvider == null)
            {
                throw new ArgumentNullException("hostIdProvider");
            }

            if (queueConfiguration == null)
            {
                throw new ArgumentNullException("queueConfiguration");
            }

            if (backgroundExceptionDispatcher == null)
            {
                throw new ArgumentNullException("backgroundExceptionDispatcher");
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

            if (log == null)
            {
                throw new ArgumentNullException("log");
            }

            _parameterName = parameterName;
            _argumentBinding = argumentBinding;
            _account = account;
            _client = account.CreateBlobClient();
            _accountName = BlobClient.GetAccountName(_client);
            _path = path;
            _hostIdProvider = hostIdProvider;
            _queueConfiguration = queueConfiguration;
            _backgroundExceptionDispatcher = backgroundExceptionDispatcher;
            _blobWrittenWatcherSetter = blobWrittenWatcherSetter;
            _messageEnqueuedWatcherSetter = messageEnqueuedWatcherSetter;
            _sharedContextProvider = sharedContextProvider;
            _log = log;
            _converter = CreateConverter(_client);
            _bindingDataContract = CreateBindingDataContract(path);
        }

        public Type TriggerValueType
        {
            get
            {
                return typeof(IStorageBlob);
            }
        }

        public IReadOnlyDictionary<string, Type> BindingDataContract
        {
            get { return _bindingDataContract; }
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

        private static IReadOnlyDictionary<string, Type> CreateBindingDataContract(IBlobPathSource path)
        {
            Dictionary<string, Type> contract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            contract.Add("BlobTrigger", typeof(string));

            IReadOnlyDictionary<string, Type> contractFromPath = path.CreateBindingDataContract();

            if (contractFromPath != null)
            {
                foreach (KeyValuePair<string, Type> item in contractFromPath)
                {
                    // In case of conflict, binding data from the value type overrides the built-in binding data above.
                    contract[item.Key] = item.Value;
                }
            }

            return contract;
        }

        private static IAsyncObjectToTypeConverter<IStorageBlob> CreateConverter(IStorageBlobClient client)
        {
            return new CompositeAsyncObjectToTypeConverter<IStorageBlob>(
                new OutputConverter<IStorageBlob>(new AsyncConverter<IStorageBlob, IStorageBlob>(
                    new IdentityConverter<IStorageBlob>())),
                new OutputConverter<ICloudBlob>(new AsyncConverter<ICloudBlob, IStorageBlob>(
                    new CloudBlobToStorageBlobConverter())),
                new OutputConverter<string>(new StringToStorageBlobConverter(client)));
        }

        public async Task<ITriggerData> BindAsync(IStorageBlob value, ValueBindingContext context)
        {
            IValueProvider valueProvider = await _argumentBinding.BindAsync(value, context);
            IReadOnlyDictionary<string, object> bindingData = CreateBindingData(value);

            return new TriggerData(valueProvider, bindingData);
        }

        public async Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            ConversionResult<IStorageBlob> conversionResult = await _converter.TryConvertAsync(value,
                context.CancellationToken);

            if (!conversionResult.Succeeded)
            {
                throw new InvalidOperationException("Unable to convert trigger to IStorageBlob.");
            }

            return await BindAsync(conversionResult.Result, context);
        }

        public IListenerFactory CreateListenerFactory(FunctionDescriptor descriptor, ITriggeredFunctionExecutor<IStorageBlob> executor)
        {
            IStorageBlobContainer container = _client.GetContainerReference(_path.ContainerNamePattern);

            IListenerFactory listenerFactory = new BlobListenerFactory(_hostIdProvider, _queueConfiguration,
                _backgroundExceptionDispatcher, _blobWrittenWatcherSetter, _messageEnqueuedWatcherSetter,
                _sharedContextProvider, _log, descriptor.Id, _account, container, _path, executor);

            return listenerFactory;
        }

        public IListenerFactory CreateListenerFactory(FunctionDescriptor descriptor, ITriggeredFunctionExecutor executor)
        {
            return CreateListenerFactory(descriptor, (ITriggeredFunctionExecutor<IStorageBlob>)executor);
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

        private IReadOnlyDictionary<string, object> CreateBindingData(IStorageBlob value)
        {
            Dictionary<string, object> bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            bindingData.Add("BlobTrigger", value.GetBlobPath());

            IReadOnlyDictionary<string, object> bindingDataFromPath = _path.CreateBindingData(value.ToBlobPath());

            if (bindingDataFromPath != null)
            {
                foreach (KeyValuePair<string, object> item in bindingDataFromPath)
                {
                    // In case of conflict, binding data from the value type overrides the built-in binding data above.
                    bindingData[item.Key] = item.Value;
                }
            }
            return bindingData;
        }
    }
}
