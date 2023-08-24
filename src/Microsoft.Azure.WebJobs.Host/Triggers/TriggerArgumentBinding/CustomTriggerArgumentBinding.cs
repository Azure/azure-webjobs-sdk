// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Bindings;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Triggers
{
    /// <summary>
    /// Bind TMessage --> TUserType. Use the specified custom converter for the conversion
    /// to TUserType. Populate binding contract with TUserType members.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <typeparam name="TTriggerValue"></typeparam>
    internal class CustomTriggerArgumentBinding<TMessage, TTriggerValue> :
        SimpleTriggerArgumentBinding<TMessage, TTriggerValue>
    {
        private readonly IBindingDataProvider _bindingDataProvider;
        private readonly FuncAsyncConverter _converter;

        public CustomTriggerArgumentBinding(
            ITriggerBindingStrategy<TMessage, TTriggerValue> bindingStrategy,
            IConverterManager converterManager,
            FuncAsyncConverter converter,
            Type userType) :
            base(bindingStrategy, converterManager)
        {
            if (converter == null)
            {
                throw new ArgumentNullException(nameof(converter));
            }
            this._converter = converter;
            this.ElementType = userType;

            _bindingDataProvider = BindingDataProvider.FromType(ElementType);
            AddToBindingContract(_bindingDataProvider);
        }

        internal override async Task<object> ConvertAsync(
            TMessage value,
            Dictionary<string, object> bindingData,
            ValueBindingContext context)
        {
            var obj = await _converter(value, null, context);

            AddToBindingData(_bindingDataProvider, bindingData, obj);

            return obj;
        }
    }
}