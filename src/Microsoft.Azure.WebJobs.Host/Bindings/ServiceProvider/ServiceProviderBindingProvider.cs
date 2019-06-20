// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    /// <summary>
    /// Binding provider for registered services in <see cref="IServiceProvider"/>.
    /// </summary>
    internal class ServiceProviderBindingProvider : IBindingProvider
    {
        private readonly IServiceProvider _serviceProvider;

        public ServiceProviderBindingProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            return Task.FromResult<IBinding>(new ServiceProviderBinding(context.Parameter, _serviceProvider));
        }
    }
}