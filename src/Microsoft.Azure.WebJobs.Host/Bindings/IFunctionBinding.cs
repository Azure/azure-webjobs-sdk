// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Executors;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    internal interface IFunctionBinding
    {
        Task<IReadOnlyDictionary<string, IValueProvider>> BindAsync(ValueBindingContext context, IDictionary<string, object> parameters);
    }

    internal interface IFunctionBindingData : IFunctionBinding
    {
        Task<IReadOnlyDictionary<string, InstrumentableObjectMetadata>> GetBindingDataAsync(ValueBindingContext context, IDictionary<string, object> parameters);
    }
}
