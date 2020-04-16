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
        private readonly bool _cacheTrigger;

        public TriggerBindingSource(ITriggeredFunctionBinding<TTriggerValue> functionBinding, TTriggerValue value, bool cacheTrigger = false)
        {
            _functionBinding = functionBinding;
            _value = value;
            _cacheTrigger = cacheTrigger;
        }

        public Task<IReadOnlyDictionary<string, IValueProvider>> BindAsync(ValueBindingContext context)
        {
            return _functionBinding.BindAsync(context, _value, _cacheTrigger);
        }
        
        public Task<IReadOnlyDictionary<string, InstrumentableObjectMetadata>> GetBindingDataAsync(ValueBindingContext context)
        {
            if (_functionBinding is ITriggeredFunctionBindingData<TTriggerValue>)
            {
                ITriggeredFunctionBindingData<TTriggerValue> functionBindingData = (ITriggeredFunctionBindingData<TTriggerValue>)_functionBinding;
                return functionBindingData.GetBindingDataAsync(context, _value, _cacheTrigger);
            }
            else
            {
                // TODO fix this
                return null;
            }
        }
    }
}
