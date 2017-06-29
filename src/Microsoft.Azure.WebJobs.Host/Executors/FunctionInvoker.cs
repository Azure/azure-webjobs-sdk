// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class FunctionInvoker<TReflected> : IFunctionInvoker
    {
        private readonly IReadOnlyList<string> _parameterNames;
        private readonly IFactory<TReflected> _instanceFactory;
        private readonly IMethodInvoker<TReflected> _methodInvoker;
        private TReflected _instance;

        public FunctionInvoker(IReadOnlyList<string> parameterNames, IFactory<TReflected> instanceFactory,
            IMethodInvoker<TReflected> methodInvoker)
        {
            if (parameterNames == null)
            {
                throw new ArgumentNullException("parameterNames");
            }

            if (instanceFactory == null)
            {
                throw new ArgumentNullException("instanceFactory");
            }

            if (methodInvoker == null)
            {
                throw new ArgumentNullException("methodInvoker");
            }

            _parameterNames = parameterNames;
            _instanceFactory = instanceFactory;
            _methodInvoker = methodInvoker;
        }

        public IFactory<TReflected> InstanceFactory
        {
            get { return _instanceFactory; }
        }

        public IReadOnlyList<string> ParameterNames
        {
            get { return _parameterNames; }
        }

        public object Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = _instanceFactory.Create();
                }
                return _instance;
            }
        }

        public async Task InvokeAsync(object[] arguments)
        {
            // Return a task immediately in case the method is not async.
            await Task.Yield();

            using (Instance as IDisposable)
            {
                await _methodInvoker.InvokeAsync((TReflected)Instance, arguments);
            }
        }
    }
}
