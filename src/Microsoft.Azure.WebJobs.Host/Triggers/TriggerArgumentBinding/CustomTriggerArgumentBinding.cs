// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Bindings;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Triggers
{
    // Bind TMessage --> TUserType. Use IConverterManager for the conversion. 
    // TUserType = the parameter type. 
    internal class CustomTriggerArgumentBinding<TMessage, TTriggerValue, TUserType> : 
        SimpleTriggerArgumentBinding<TMessage, TTriggerValue>
    {
        private readonly FuncAsyncConverter<TMessage, TUserType> _converter;

        public CustomTriggerArgumentBinding(
            ITriggerBindingStrategy<TMessage, TTriggerValue> bindingStrategy, 
            IConverterManager converterManager,
            FuncAsyncConverter<TMessage, TUserType> converter) :
            base(bindingStrategy, converterManager)
        {
            if (converter == null)
            {
                throw new ArgumentNullException("converter");
            }
            this._converter = converter;
            this.ElementType = typeof(TUserType);
        }

        internal override async Task<object> ConvertAsync(
            TMessage value, 
            Dictionary<string, object> bindingData,
            ValueBindingContext context)
        {
            var obj = await _converter(value, null, context);
            return obj;
        }
    }
}