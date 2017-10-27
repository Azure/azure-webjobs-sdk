// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.Host
{
    // Bind EventData to itself 
    internal class SimpleTriggerArgumentBinding<TMessage, TTriggerValue> : ITriggerDataArgumentBinding<TTriggerValue>
    {
        private readonly ITriggerBindingStrategy<TMessage, TTriggerValue> _hooks;
        private readonly IConverterManager _converterManager;
        private readonly FuncAsyncConverter<TMessage, string> _stringConverter;

        public SimpleTriggerArgumentBinding(
            ITriggerBindingStrategy<TMessage, TTriggerValue> hooks, 
            IConverterManager converterManager, 
            bool isSingleDispatch = true)
        {
            this._hooks = hooks;
            this.Contract = Hooks.GetBindingContract(isSingleDispatch);
            this.ElementType = typeof(TMessage);
            _converterManager = converterManager;
            _stringConverter = _converterManager.GetConverter<TMessage, string, Attribute>();
        }

        // Caller can set it
        protected Dictionary<string, Type> Contract { get; set; }
        protected internal Type ElementType { get; set; }

        protected ITriggerBindingStrategy<TMessage, TTriggerValue> Hooks
        {
            get
            {
                return _hooks;
            }
        }

        IReadOnlyDictionary<string, Type> ITriggerDataArgumentBinding<TTriggerValue>.BindingDataContract
        {
            get
            {
                return Contract;
            }
        }

        public Type ValueType
        {
            get
            {
                return typeof(TTriggerValue);
            }
        }

        internal virtual Task<object> ConvertAsync(TMessage value, Dictionary<string, object> bindingData, ValueBindingContext context)
        {
            return Task.FromResult<object>(value);
        }

        protected async Task<string> ConvertToStringAsync(TMessage eventData)
        {
            var val = await _stringConverter(eventData, null, null);
            return val;
        }

        public virtual async Task<ITriggerData> BindAsync(TTriggerValue value, ValueBindingContext context)
        {
            Dictionary<string, object> bindingData = Hooks.GetBindingData(value);

            TMessage eventData = Hooks.BindSingle(value, context);

            object userValue = await this.ConvertAsync(eventData, bindingData, context);

            string invokeString = await ConvertToStringAsync(eventData);

            IValueProvider valueProvider = new ConstantValueProvider(userValue, this.ElementType, invokeString);
            var triggerData = new TriggerData(valueProvider, bindingData);

            return triggerData;
        }
    }
}