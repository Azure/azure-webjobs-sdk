// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    internal class ServiceProviderBinding : IBinding
    {
        private readonly ParameterInfo _parameter;
        private readonly IServiceProvider _serviceProvider;

        public ServiceProviderBinding(ParameterInfo parameter, IServiceProvider serviceProvider)
        {
            _parameter = parameter;
            _serviceProvider = serviceProvider;
        }

        public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
        {
            return Task.FromResult<IValueProvider>(new ObjectValueProvider(value, _parameter.ParameterType));
        }

        public Task<IValueProvider> BindAsync(BindingContext context)
        {
            return BindAsync(_serviceProvider.GetRequiredService(_parameter.ParameterType), context.ValueContext);
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new ParameterDescriptor
            {
                Name = _parameter.Name
            };
        }

        public bool FromAttribute => false;
    }
}