﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.ServiceBus.Config;

namespace Microsoft.Azure.WebJobs.ServiceBus.Bindings
{
    internal class ServiceBusAttributeBindingProvider : IBindingProvider
    {
        private static readonly IQueueArgumentBindingProvider _innerProvider =
            new CompositeArgumentBindingProvider(
                new BrokeredMessageArgumentBindingProvider(),
                new StringArgumentBindingProvider(),
                new ByteArrayArgumentBindingProvider(),
                new UserTypeArgumentBindingProvider(),
                new CollectorArgumentBindingProvider(),
                new AsyncCollectorArgumentBindingProvider());

        private readonly INameResolver _nameResolver;
        private readonly ServiceBusConfiguration _config;

        public ServiceBusAttributeBindingProvider(INameResolver nameResolver, ServiceBusConfiguration config)
        {
            if (nameResolver == null)
            {
                throw new ArgumentNullException("nameResolver");
            }
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            _nameResolver = nameResolver;
            _config = config;
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            ParameterInfo parameter = context.Parameter;
            ServiceBusAttribute serviceBusAttribute = parameter.GetCustomAttribute<ServiceBusAttribute>(inherit: false);

            if (serviceBusAttribute == null)
            {
                return Task.FromResult<IBinding>(null);
            }

            string queueOrTopicName = Resolve(serviceBusAttribute.QueueOrTopicName);
            IBindableServiceBusPath path = BindableServiceBusPath.Create(queueOrTopicName);
            ValidateContractCompatibility(path, context.BindingDataContract);

            IArgumentBinding<ServiceBusEntity> argumentBinding = _innerProvider.TryCreate(parameter);

            if (argumentBinding == null)
            {
                throw new InvalidOperationException("Can't bind ServiceBus to type '" + parameter.ParameterType + "'.");
            }

            ServiceBusAccount account = ServiceBusAccount.CreateFromConnectionString(_config.ConnectionString);

            IBinding binding = new ServiceBusBinding(parameter.Name, argumentBinding, account, path);
            return Task.FromResult(binding);
        }

        private static void ValidateContractCompatibility(IBindableServiceBusPath path, IReadOnlyDictionary<string, Type> bindingDataContract)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            IEnumerable<string> parameterNames = path.ParameterNames;
            if (parameterNames != null)
            {
                foreach (string parameterName in parameterNames)
                {
                    if (bindingDataContract != null && !bindingDataContract.ContainsKey(parameterName))
                    {
                        throw new InvalidOperationException(string.Format("No binding parameter exists for '{0}'.", parameterName));
                    }
                }
            }
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
