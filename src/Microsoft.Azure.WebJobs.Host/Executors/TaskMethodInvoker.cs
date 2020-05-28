// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class TaskMethodInvoker<TReflected, TReturnType> : IMethodInvoker<TReflected, TReturnType>
    {
        private readonly Func<TReflected, object[], Task<TReturnType>> _lambda;

        public TaskMethodInvoker(Func<TReflected, object[], Task<TReturnType>> lambda)
        {
            _lambda = lambda;
        }

        public Task<TReturnType> InvokeAsync(TReflected instance, object[] arguments)
        {
            return _lambda.Invoke(instance, arguments);
        }
    }
}
