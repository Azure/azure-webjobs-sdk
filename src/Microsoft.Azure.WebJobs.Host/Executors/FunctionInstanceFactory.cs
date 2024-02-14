// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class FunctionInstanceFactory : IFunctionInstanceFactory
    {
        private readonly IFunctionBinding _binding;
        private readonly IFunctionInvoker _invoker;
        private readonly FunctionDescriptor _descriptor;
        private readonly IInstanceServicesProviderFactory _instanceServicesProviderFactory;

        public FunctionInstanceFactory(IFunctionBinding binding, IFunctionInvoker invoker, FunctionDescriptor descriptor, IInstanceServicesProviderFactory instanceServicesFactory)
        {
            _binding = binding;
            _invoker = invoker;
            _descriptor = descriptor;
            _instanceServicesProviderFactory = instanceServicesFactory;
        }

        public IFunctionInstance Create(FunctionInstanceFactoryContext context)
        {
            IBindingSource bindingSource = new BindingSource(_binding, context.Parameters);
            // The internal implementation of the FunctionInstance class will dispose of the created instance services provider when the function instance is disposed.
            return new FunctionInstance(context, bindingSource, _invoker, _descriptor, _instanceServicesProviderFactory);
        }
    }
}