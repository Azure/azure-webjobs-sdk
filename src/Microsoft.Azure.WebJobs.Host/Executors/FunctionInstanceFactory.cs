// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class FunctionInstanceFactory : IFunctionInstanceFactory
    {
        private readonly IFunctionBinding _binding;
        private readonly IFunctionInvoker _invoker;
        private readonly FunctionDescriptor _descriptor;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public FunctionInstanceFactory(IFunctionBinding binding, IFunctionInvoker invoker, FunctionDescriptor descriptor, IServiceScopeFactory serviceScopeFactory)
        {
            _binding = binding;
            _invoker = invoker;
            _descriptor = descriptor;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public IFunctionInstance Create(FunctionInstanceFactoryContext context)
        {
            IBindingSource bindingSource = new BindingSource(_binding, context.Parameters);
            return new FunctionInstance(context.Id, context.TriggerDetails, context.ParentId, context.ExecutionReason, bindingSource, _invoker, _descriptor, _serviceScopeFactory);
        }
    }
}
