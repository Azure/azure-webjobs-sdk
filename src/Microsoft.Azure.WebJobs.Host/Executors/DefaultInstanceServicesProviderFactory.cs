// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class DefaultInstanceServicesProviderFactory : IInstanceServicesProviderFactory
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public DefaultInstanceServicesProviderFactory(IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }

        public IInstanceServicesProvider CreateInstanceServicesProvider(FunctionInstanceFactoryContext functionInstanceFactoryContext)
        {
            return new DefaultInstanceServicesProvider(_serviceScopeFactory);
        }
    }
}