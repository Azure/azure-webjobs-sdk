// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class FunctionInstance : IFunctionInstanceEx, IDisposable
    {
        private IServiceScope _instanceServicesScope;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private IServiceProvider _instanceServices;

        public FunctionInstance(Guid id, IDictionary<string, string> triggerDetails, Guid? parentId, ExecutionReason reason, IBindingSource bindingSource,
            IFunctionInvoker invoker, FunctionDescriptor functionDescriptor, IServiceScopeFactory serviceScopeFactory)
        {
            Id = id;
            TriggerDetails = triggerDetails;
            ParentId = parentId;
            Reason = reason;
            BindingSource = bindingSource;
            Invoker = invoker;
            FunctionDescriptor = functionDescriptor;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public Guid Id { get; }

        public IDictionary<string, string> TriggerDetails { get; }

        public Guid? ParentId { get; }

        public ExecutionReason Reason { get; }

        public IBindingSource BindingSource { get; }

        public IFunctionInvoker Invoker { get; }

        public FunctionDescriptor FunctionDescriptor { get; }

        public IServiceProvider InstanceServices
        {
            get
            {
                if (_instanceServicesScope == null && _serviceScopeFactory != null)
                {
                    _instanceServicesScope = _serviceScopeFactory.CreateScope();
                    _instanceServices = _instanceServicesScope.ServiceProvider;
                }

                return _instanceServices;
            }
        }

        public void Dispose()
        {
            if (_instanceServicesScope != null)
            {
                _instanceServicesScope.Dispose();
            }

            _instanceServicesScope = null;
            _instanceServices = null;
        }
    }
}
