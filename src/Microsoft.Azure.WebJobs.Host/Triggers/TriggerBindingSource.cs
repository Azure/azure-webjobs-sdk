// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.Host.Triggers
{
    internal class TriggerBindingSource<TTriggerValue> : IBindingData
    {
        private readonly ITriggeredFunctionBinding<TTriggerValue> _functionBinding;
        private readonly TTriggerValue _value;

        public TriggerBindingSource(ITriggeredFunctionBinding<TTriggerValue> functionBinding, TTriggerValue value)
        {
            _functionBinding = functionBinding;
            _value = value;
        }

        public Task<IReadOnlyDictionary<string, IValueProvider>> BindAsync(ValueBindingContext context)
        {
            return _functionBinding.BindAsync(context, _value);
        }
        
        public Task<IReadOnlyDictionary<string, InstrumentableObjectMetadata>> GetBindingDataAsync(ValueBindingContext context)
        {
            if (_functionBinding is ITriggeredFunctionBindingData<TTriggerValue>)
            {
                ITriggeredFunctionBindingData<TTriggerValue> functionBindingData = (ITriggeredFunctionBindingData<TTriggerValue>)_functionBinding;
                return functionBindingData.GetBindingDataAsync(context, _value);
            }
            else
            {
                // TODO fix this
                return null;
            }
        }
    }
}
