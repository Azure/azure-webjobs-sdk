// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Bindings;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    // $$$ Can we merge with C:\dev\AFunc\azure-webjobs-sdk\src\Microsoft.Azure.WebJobs.ServiceBus\Triggers\UserTypeArgumentBindingProvider.cs ?
    // This needs to populate the binding contract with the properties of the object. 
    class PocoTriggerArgumentBinding<TMessage, TTriggerValue> : StringTriggerArgumentBinding<TMessage, TTriggerValue>
    {
        IBindingDataProvider _provider;

        public PocoTriggerArgumentBinding(ITriggerBindingStrategy<TMessage, TTriggerValue> hooks, Type elementType) : base(hooks)
        {
            this._elementType = elementType;

            // Add properties ot binding data 
            _provider = BindingDataProvider.FromType(elementType);

            // Binding data from Poco properties takes precedence over builtins
            foreach (var kv in _provider.Contract)
            {
                string name = kv.Key;
                Type type = kv.Value;
                _contract[name] = type;
            }
        }

        internal override object Convert(TMessage value, Dictionary<string, object> bindingData)
        {
            string json = _hooks.ConvertEventData2String(value);
            var obj = JsonConvert.DeserializeObject(json, this._elementType);

            if (bindingData != null)
            {
                var pocoData = _provider.GetBindingData(obj);

                foreach (var kv in pocoData)
                {
                    string propName = kv.Key;
                    object propVal = kv.Value;
                    bindingData[propName] = propVal;
                }
            }

            return obj;
        }
    }
}