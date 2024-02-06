// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class FunctionInstance : IFunctionInstanceEx, IDisposable
    {
        private IInstanceServicesProviderFactory _instanceServicesProviderFactory;
        private IInstanceServicesProvider _instanceServicesProvider;
        private readonly FunctionInstanceFactoryContext _instanceContext;

        public FunctionInstance(FunctionInstanceFactoryContext context, IBindingSource bindingSource,
                                IFunctionInvoker invoker, FunctionDescriptor functionDescriptor,
                                IInstanceServicesProviderFactory instanceServicesProviderFactory)
        {
            
            _instanceContext = context ?? throw new ArgumentNullException(nameof(context));
            _instanceServicesProviderFactory = instanceServicesProviderFactory;

            BindingSource = bindingSource;
            Invoker = invoker;
            FunctionDescriptor = functionDescriptor;
        }


        public Guid Id => _instanceContext.Id;

        public IDictionary<string, string> TriggerDetails => _instanceContext.TriggerDetails;

        public Guid? ParentId => _instanceContext.ParentId;

        public ExecutionReason Reason => _instanceContext.ExecutionReason;

        public IBindingSource BindingSource { get; }

        public IFunctionInvoker Invoker { get; }

        public FunctionDescriptor FunctionDescriptor { get; }

        public RetryContext RetryContext { get; set; }

        public IServiceProvider InstanceServices
        {
            get
            {
                if (_instanceServicesProvider == null && _instanceServicesProviderFactory != null)
                {
                    _instanceServicesProvider = _instanceServicesProviderFactory.CreateInstanceServicesProvider(_instanceContext);
                }

                return _instanceServicesProvider?.InstanceServices;
            }
        }

        public void Dispose()
        {
            (_instanceServicesProvider as IDisposable)?.Dispose();

            _instanceServicesProvider = null;
        }
    }
}