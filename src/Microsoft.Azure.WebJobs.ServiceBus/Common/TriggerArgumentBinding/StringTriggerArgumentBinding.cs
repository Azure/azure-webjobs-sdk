// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.ServiceBus
{

    // $$$ Use Conversion Manager
    // Bind EventData --> String
    class StringTriggerArgumentBinding<TMessage, TTriggerValue> : SimpleTriggerArgumentBinding<TMessage, TTriggerValue>
    {
        public StringTriggerArgumentBinding(ITriggerBindingStrategy<TMessage, TTriggerValue> hooks) : base(hooks)
        {
            this._elementType = typeof(string);
        }

        internal override object Convert(TMessage value, Dictionary<string, object> bindingData)
        {
            var obj = _hooks.ConvertEventData2String(value);
            return obj;
        }
    }
}