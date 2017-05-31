// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class TriggeredFunctionExecutor<TTriggerValue> : ITriggeredFunctionExecutor, ITriggeredFunctionExecutorWithHook
    {
        private FunctionDescriptor _descriptor;
        private ITriggeredFunctionInstanceFactory<TTriggerValue> _instanceFactory;
        private IFunctionExecutor _executor;

        public TriggeredFunctionExecutor(FunctionDescriptor descriptor, IFunctionExecutor executor, ITriggeredFunctionInstanceFactory<TTriggerValue> instanceFactory)
        {
            _descriptor = descriptor;
            _executor = executor;
            _instanceFactory = instanceFactory;
        }

        public FunctionDescriptor Function
        {
            get
            {
                return _descriptor;
            }
        }

        public Task<FunctionResult> TryExecuteAsync(TriggeredFunctionData input, CancellationToken cancellationToken)
        {
            return TryExecuteAsync(input, cancellationToken, null);
        }

        public async Task<FunctionResult> TryExecuteAsync(TriggeredFunctionData input, CancellationToken cancellationToken, Func<Func<Task>, Task> hook)
        {
            IFunctionInstance instance = _instanceFactory.Create((TTriggerValue)input.TriggerValue, input.ParentId);
            if (hook != null)
            {
                IFunctionInvoker invoker = new InvokeWrapper(instance.Invoker, hook);
                instance = new FunctionInstance(instance.Id, instance.ParentId, instance.Reason, instance.BindingSource, invoker, instance.FunctionDescriptor);
            }

            IDelayedException exception = await _executor.TryExecuteAsync(instance, cancellationToken);

            FunctionResult result = exception != null ?
                new FunctionResult(exception.Exception)
                : new FunctionResult(true);

            return result;
        }

        private class InvokeWrapper : IFunctionInvoker
        {
            private readonly IFunctionInvoker _inner;
            private readonly Func<Func<Task>, Task> _hook;

            public InvokeWrapper(IFunctionInvoker inner, Func<Func<Task>, Task> hook)
            {
                _inner = inner;
                _hook = hook;
            }
            public IReadOnlyList<string> ParameterNames => _inner.ParameterNames;

            public Task InvokeAsync(object[] arguments)
            {
                Func<Task> inner = () => _inner.InvokeAsync(arguments);
                return _hook(inner);
            }
        }
    }
}
