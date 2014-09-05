﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Host.Triggers
{
    internal class TriggeredFunctionInstanceFactory<TTriggerValue> : ITriggeredFunctionInstanceFactory<TTriggerValue>
    {
        private readonly ITriggeredFunctionBinding<TTriggerValue> _binding;
        private readonly IInvoker _invoker;
        private readonly FunctionDescriptor _descriptor;
        private readonly MethodInfo _method;

        public TriggeredFunctionInstanceFactory(ITriggeredFunctionBinding<TTriggerValue> binding, IInvoker invoker,
            FunctionDescriptor descriptor, MethodInfo method)
        {
            _binding = binding;
            _invoker = invoker;
            _descriptor = descriptor;
            _method = method;
        }

        public IFunctionInstance Create(TTriggerValue value, Guid? parentId)
        {
            IBindingSource bindingSource = new TriggerBindingSource<TTriggerValue>(_binding, value);
            return new FunctionInstance(Guid.NewGuid(), parentId, ExecutionReason.AutomaticTrigger, bindingSource,
                _invoker, _descriptor, _method);
        }

        public IFunctionInstance Create(Guid id, Guid? parentId, ExecutionReason reason,
            IDictionary<string, object> parameters)
        {
            IBindingSource bindingSource = new BindingSource(_binding, parameters);
            return new FunctionInstance(id, parentId, reason, bindingSource, _invoker, _descriptor, _method);
        }
    }
}
