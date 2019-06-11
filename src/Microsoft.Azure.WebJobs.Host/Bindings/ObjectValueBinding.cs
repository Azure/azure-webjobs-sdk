// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Protocols;
using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    class ObjectValueBinding : IBinding
    {
        private readonly object _value;
        private readonly Type _valueType;
        private readonly bool _fromAttribute;
        private readonly ParameterDescriptor _parameterDescriptor;

        public ObjectValueBinding(object value, Type valueType, bool fromAttribute, ParameterDescriptor parameterDescriptor)
        {
            if (value != null && !valueType.IsAssignableFrom(value.GetType()))
            {
                throw new InvalidOperationException("value is not of the correct type.");
            }

            _value = value;
            _valueType = valueType;
            _fromAttribute = fromAttribute;
            _parameterDescriptor = parameterDescriptor;
        }

        public bool FromAttribute => _fromAttribute;

        public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
        {
            return Task.FromResult(new ObjectValueProvider(_value, _valueType) as IValueProvider);
        }


        public Task<IValueProvider> BindAsync(BindingContext context)
        {
            return Task.FromResult(new ObjectValueProvider(_value, _valueType) as IValueProvider);
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return _parameterDescriptor;
        }
    }
}
