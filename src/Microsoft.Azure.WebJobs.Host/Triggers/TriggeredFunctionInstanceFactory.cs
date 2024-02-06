// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Host.Triggers
{
    internal class TriggeredFunctionInstanceFactory<TTriggerValue> : ITriggeredFunctionInstanceFactory<TTriggerValue>
    {
        private readonly ITriggeredFunctionBinding<TTriggerValue> _binding;
        private readonly IFunctionInvokerEx _invoker;
        private readonly FunctionDescriptor _descriptor;
        private readonly IInstanceServicesProviderFactory _instanceServicesProviderFactory;

        public TriggeredFunctionInstanceFactory(ITriggeredFunctionBinding<TTriggerValue> binding,
            IFunctionInvokerEx invoker, FunctionDescriptor descriptor, IInstanceServicesProviderFactory instanceServicesProviderFactory)
        {
            _binding = binding;
            _invoker = invoker;
            _descriptor = descriptor;
            _instanceServicesProviderFactory = instanceServicesProviderFactory;
        }

        public IFunctionInstance Create(FunctionInstanceFactoryContext<TTriggerValue> context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var bindingSource = new TriggerBindingSource<TTriggerValue>(_binding, context.TriggerValue);
            var invoker = CreateInvoker(context);

            return new FunctionInstance(context, bindingSource, invoker, _descriptor, _instanceServicesProviderFactory);
        }

        public IFunctionInstance Create(FunctionInstanceFactoryContext context)
        {
            IBindingSource bindingSource = new BindingSource(_binding, context.Parameters);
            var invoker = CreateInvoker(context);

            return new FunctionInstance(context, bindingSource, invoker, _descriptor, _instanceServicesProviderFactory);
        }

        private IFunctionInvokerEx CreateInvoker(FunctionInstanceFactoryContext context)
        {
            if (context.InvokeHandler != null)
            {
                return new InvokeWrapper(_invoker, context.InvokeHandler);
            }
            else
            {
                return _invoker;
            }
        }

        private class InvokeWrapper : IFunctionInvokerEx
        {
            private readonly IFunctionInvokerEx _inner;
            private readonly Func<Func<Task<object>>, Task<object>> _handler;

            public InvokeWrapper(IFunctionInvokerEx inner, Func<Func<Task<object>>, Task<object>> handler)
            {
                _inner = inner;
                _handler = handler;
            }
            public IReadOnlyList<string> ParameterNames => _inner.ParameterNames;

            public Task<object> InvokeAsync(object instance, object[] arguments)
            {
                Func<Task<object>> inner = () => _inner.InvokeAsync(instance, arguments);
                return _handler(inner);
            }

            public object CreateInstance(IFunctionInstanceEx functionInstance)
            {
                return _inner.CreateInstance(functionInstance);
            }

            public object CreateInstance()
            {
                throw new NotSupportedException($"{nameof(CreateInstance)} is not supported. Please use the overload that accepts an instance of an {nameof(IFunctionInstance)}");
            }
        }
    }
}