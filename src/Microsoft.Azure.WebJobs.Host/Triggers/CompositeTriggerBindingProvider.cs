// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Host.Triggers
{
    internal class CompositeTriggerBindingProvider : ITriggerBindingProvider, IBindingProviderX
    {
        private readonly IEnumerable<ITriggerBindingProvider> _providers;

        public CompositeTriggerBindingProvider(IEnumerable<ITriggerBindingProvider> providers)
        {
            _providers = providers;
        }

        public Type GetDefaultType(FileAccess access, Cardinality cardinality, DataType dataType, Attribute attr)
        {
            foreach (ITriggerBindingProvider provider in _providers)
            {
                var binding = provider as IBindingProviderX;
                if (binding != null)
                {
                    var type = binding.GetDefaultType(access, cardinality, dataType, attr);
                    if (type != null)
                    {
                        return type;
                    }
                }
            }

            return null;
        }

        public async Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            foreach (ITriggerBindingProvider provider in _providers)
            {
                ITriggerBinding binding = await provider.TryCreateAsync(context);

                if (binding != null)
                {
                    return binding;
                }
            }

            return null;
        }
    }
}
