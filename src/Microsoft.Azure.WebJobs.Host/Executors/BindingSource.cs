// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class BindingSource : IBindingData
    {
        private readonly IFunctionBinding _binding;
        private readonly IDictionary<string, object> _parameters;
        private readonly bool _cacheTrigger;

        public BindingSource(IFunctionBinding binding, IDictionary<string, object> parameters, bool cacheTrigger = false)
        {
            _binding = binding;
            _parameters = parameters;
            _cacheTrigger = cacheTrigger;
        }

        public Task<IReadOnlyDictionary<string, IValueProvider>> BindAsync(ValueBindingContext context)
        {
            return _binding.BindAsync(context, _parameters, _cacheTrigger);
        }

        public Task<IReadOnlyDictionary<string, InstrumentableObjectMetadata>> GetBindingDataAsync(ValueBindingContext context)
        {
            if (_binding is IFunctionBindingData)
            {
                IFunctionBindingData functionBindingData = (IFunctionBindingData)_binding;
                return functionBindingData.GetBindingDataAsync(context, _parameters, _cacheTrigger);
            }
            else
            {
                // TODO fix this
                return null;
            }
        }
    }
}
