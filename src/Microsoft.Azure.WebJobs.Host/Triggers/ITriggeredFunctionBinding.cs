// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.Host.Triggers
{
    internal interface ITriggeredFunctionBinding<TTriggerValue> : IFunctionBindingData
    {
        Task<IReadOnlyDictionary<string, IValueProvider>> BindAsync(ValueBindingContext context, TTriggerValue value);
    }

    internal interface ITriggeredFunctionBindingData<TTriggerValue> : ITriggeredFunctionBinding<TTriggerValue>
    {
        Task<IReadOnlyDictionary<string, InstrumentableObjectMetadata>> GetBindingDataAsync(ValueBindingContext context, TTriggerValue value);
    }
}
