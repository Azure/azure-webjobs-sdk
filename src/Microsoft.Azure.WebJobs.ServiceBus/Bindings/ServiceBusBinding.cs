﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Converters;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.ServiceBus.Bindings
{
    internal class ServiceBusBinding : IBinding
    {
        private readonly string _parameterName;
        private readonly IArgumentBinding<ServiceBusEntity> _argumentBinding;
        private readonly ServiceBusAccount _account;
        private readonly IBindableServiceBusPath _path;
        private readonly IAsyncObjectToTypeConverter<ServiceBusEntity> _converter;
        private readonly EntityType _entityType;

        public ServiceBusBinding(string parameterName, IArgumentBinding<ServiceBusEntity> argumentBinding, ServiceBusAccount account, IBindableServiceBusPath path, ServiceBusAttribute attr)
        {
            _parameterName = parameterName;
            _argumentBinding = argumentBinding;
            _account = account;
            _path = path;
            _entityType = attr.EntityType;
            _converter = new OutputConverter<string>(
                new StringToServiceBusEntityConverter(account, _path, _entityType));
        }

        public bool FromAttribute
        {
            get { return true; }
        }

        public async Task<IValueProvider> BindAsync(BindingContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            string boundQueueName = _path.Bind(context.BindingData);
            var messageSender = new MessageSender(_account.ConnectionString, boundQueueName);

            var entity = new ServiceBusEntity
            {
                MessageSender = messageSender,
                EntityType = _entityType
            };

            return await BindAsync(entity, context.ValueContext);
        }

        public async Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
        {
            ConversionResult<ServiceBusEntity> conversionResult = await _converter.TryConvertAsync(value, context.CancellationToken);

            if (!conversionResult.Succeeded)
            {
                throw new InvalidOperationException("Unable to convert value to ServiceBusEntity.");
            }

            return await BindAsync(conversionResult.Result, context);
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new ServiceBusParameterDescriptor
            {
                Name = _parameterName,
                QueueOrTopicName = _path.QueueOrTopicNamePattern,
                DisplayHints = CreateParameterDisplayHints(_path.QueueOrTopicNamePattern, false)
            };
        }

        private Task<IValueProvider> BindAsync(ServiceBusEntity value, ValueBindingContext context)
        {
            return _argumentBinding.BindAsync(value, context);
        }

        internal static ParameterDisplayHints CreateParameterDisplayHints(string entityPath, bool isInput)
        {
            ParameterDisplayHints descriptor = new ParameterDisplayHints();

            descriptor.Description = isInput ?
                string.Format(CultureInfo.CurrentCulture, "dequeue from '{0}'", entityPath) :
                string.Format(CultureInfo.CurrentCulture, "enqueue to '{0}'", entityPath);

            descriptor.Prompt = isInput ?
                "Enter the queue message body" :
                "Enter the output entity name";

            descriptor.DefaultValue = isInput ? null : entityPath;

            return descriptor;
        }
    }
}
