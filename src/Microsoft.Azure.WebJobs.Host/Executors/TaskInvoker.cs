// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class TaskInvoker : IInvoker
    {
        private readonly Func<object[], Task> _lambda;

        public TaskInvoker(Func<object[], Task> lambda)
        {
            _lambda = lambda;
        }

        public Task InvokeAsync(object[] parameters)
        {
            Task task = _lambda.Invoke(parameters);

            if (task == null)
            {
                return null;
            }

            Type taskType = task.GetType();

        }

        private static void ThrowIfWrappedTaskInstance(Type type)
        {
            Debug.Assert(type != null);

            // Fast path: check if type is exactly Task first.
            if (type != typeof(Task))
            {
                Type innerTaskType = TypeHelper.GetTaskInnerTypeOrNull(type);
                if (innerTaskType != null && typeof(Task).IsAssignableFrom(innerTaskType))
                {
                    throw new InvalidOperationException("The method returned a nested task instance. Make sure to " +
                        "call Unwrap on the returned value to avoid an unobserved faulted Task.");
                }
            }
        }

        private static Type GetTaskInnerTypeOrNull(Type type)
        {
            Debug.Assert(type != null);
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                Type genericTypeDefinition = type.GetGenericTypeDefinition();

                if (TaskGenericType == genericTypeDefinition)
                {
                    return type.GetGenericArguments()[0];
                }
            }

            return null;
        }
    }
}
