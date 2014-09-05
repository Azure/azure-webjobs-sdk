﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Converters;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Queues.Listeners;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Microsoft.Azure.WebJobs.Host.Queues.Triggers
{
    internal class QueueTriggerBinding : ITriggerBinding<CloudQueueMessage>
    {
        private static readonly IObjectToTypeConverter<CloudQueueMessage> _converter =
            new CompositeObjectToTypeConverter<CloudQueueMessage>(
                new OutputConverter<CloudQueueMessage>(new IdentityConverter<CloudQueueMessage>()),
                new OutputConverter<string>(new StringToCloudQueueMessageConverter()));

        private readonly string _parameterName;
        private readonly ITriggerDataArgumentBinding<CloudQueueMessage> _argumentBinding;
        private readonly CloudStorageAccount _account;
        private readonly string _accountName;
        private readonly string _queueName;
        private readonly IReadOnlyDictionary<string, Type> _bindingDataContract;

        public QueueTriggerBinding(string parameterName, ITriggerDataArgumentBinding<CloudQueueMessage> argumentBinding,
            CloudStorageAccount account, string queueName)
        {
            _parameterName = parameterName;
            _argumentBinding = argumentBinding;
            _account = account;
            _accountName = StorageClient.GetAccountName(account);
            _queueName = queueName;
            _bindingDataContract = CreateBindingDataContract(argumentBinding);
        }

        public IReadOnlyDictionary<string, Type> BindingDataContract
        {
            get { return _bindingDataContract; }
        }

        public string QueueName
        {
            get { return _queueName; }
        }

        private static IReadOnlyDictionary<string, Type> CreateBindingDataContract(
            ITriggerDataArgumentBinding<CloudQueueMessage> argumentBinding)
        {
            Dictionary<string, Type> contract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            contract.Add("DequeueCount", typeof(int));
            contract.Add("ExpirationTime", typeof(DateTimeOffset));
            contract.Add("Id", typeof(string));
            contract.Add("InsertionTime", typeof(DateTimeOffset));
            contract.Add("NextVisibleTime", typeof(DateTimeOffset));
            contract.Add("PopReceipt", typeof(string));

            if (argumentBinding.BindingDataContract != null)
            {
                foreach (KeyValuePair<string, Type> item in argumentBinding.BindingDataContract)
                {
                    // In case of conflict, binding data from the value type overrides the built-in binding data above.
                    contract[item.Key] = item.Value;
                }
            }

            return contract;
        }

        public async Task<ITriggerData> BindAsync(CloudQueueMessage value, ValueBindingContext context)
        {
            ITriggerData triggerData = await _argumentBinding.BindAsync(value, context);
            IReadOnlyDictionary<string, object> bindingData = CreateBindingData(value, triggerData.BindingData);

            return new TriggerData(triggerData.ValueProvider, bindingData);
        }

        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            CloudQueueMessage message = null;

            if (!_converter.TryConvert(value, out message))
            {
                throw new InvalidOperationException("Unable to convert trigger to CloudQueueMessage.");
            }

            return BindAsync(message, context);
        }

        public IFunctionDefinition CreateFunctionDefinition(IReadOnlyDictionary<string, IBinding> nonTriggerBindings,
            IInvoker invoker, FunctionDescriptor functionDescriptor, MethodInfo method)
        {
            ITriggeredFunctionBinding<CloudQueueMessage> functionBinding =
                new TriggeredFunctionBinding<CloudQueueMessage>(_parameterName, this, nonTriggerBindings);
            ITriggeredFunctionInstanceFactory<CloudQueueMessage> instanceFactory =
                new TriggeredFunctionInstanceFactory<CloudQueueMessage>(functionBinding, invoker, functionDescriptor,
                    method);
            CloudQueueClient client = _account.CreateCloudQueueClient();
            CloudQueue queue = client.GetQueueReference(_queueName);
            IListenerFactory listenerFactory = new QueueListenerFactory(queue, instanceFactory);
            return new FunctionDefinition(instanceFactory, listenerFactory);
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new QueueTriggerParameterDescriptor
            {
                Name = _parameterName,
                AccountName = _accountName,
                QueueName = _queueName
            };
        }

        private IReadOnlyDictionary<string, object> CreateBindingData(CloudQueueMessage value,
            IReadOnlyDictionary<string, object> bindingDataFromValueType)
        {
            Dictionary<string, object> bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            bindingData.Add("DequeueCount", value.DequeueCount);
            bindingData.Add("ExpirationTime", value.ExpirationTime.GetValueOrDefault(DateTimeOffset.MaxValue));
            bindingData.Add("Id", value.Id);
            bindingData.Add("InsertionTime", value.InsertionTime.GetValueOrDefault(DateTimeOffset.UtcNow));
            bindingData.Add("NextVisibleTime", value.NextVisibleTime.GetValueOrDefault(DateTimeOffset.MaxValue));
            bindingData.Add("PopReceipt", value.PopReceipt);
            
            if (bindingDataFromValueType != null)
            {
                foreach (KeyValuePair<string, object> item in bindingDataFromValueType)
                {
                    // In case of conflict, binding data from the value type overrides the built-in binding data above.
                    bindingData[item.Key] = item.Value;
                }
            }

            return bindingData;
        }
    }
}
