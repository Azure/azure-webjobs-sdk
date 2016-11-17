// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    internal class CompositeBindingProvider : IBindingProvider, IBindingProviderX
    {
        private readonly IEnumerable<IBindingProvider> _providers;

        public CompositeBindingProvider(IEnumerable<IBindingProvider> providers)
        {
            _providers = providers;
        }

        public async Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            foreach (IBindingProvider provider in _providers)
            {
                IBinding binding = await provider.TryCreateAsync(context);
                if (binding != null)
                {
                    return binding;
                }
            }

            return null;
        }

        public Type GetDefaultType(FileAccess access, Cardinality cardinality, DataType dataType, Attribute attr)
        {
            foreach (var provider in _providers)
            {
                var x = provider as IBindingProviderX;
                if (x != null)
                {
                    var type = x.GetDefaultType(access, cardinality, dataType, attr);
                    if (type != null)
                    {
                        return type;
                    }
                }
            }
            return null;
        }
    }
}
