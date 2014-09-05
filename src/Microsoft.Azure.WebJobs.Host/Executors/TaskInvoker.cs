// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal class TaskInvoker : IInvoker
    {
        private readonly IReadOnlyList<string> _parameterNames;
        private readonly Func<object[], Task> _lambda;

        public TaskInvoker(IReadOnlyList<string> parameterNames, Func<object[], Task> lambda)
        {
            _parameterNames = parameterNames;
            _lambda = lambda;
        }

        public IReadOnlyList<string> ParameterNames
        {
            get { return _parameterNames; }
        }

        public Task InvokeAsync(object[] parameters)
        {
            Task task = _lambda.Invoke(parameters);

            if (task == null)
            {
                return null;
            }

            Type taskType = task.GetType();
            ThrowIfWrappedTaskInstance(taskType);
            return task;
        }

        private static void ThrowIfWrappedTaskInstance(Type taskType)
        {
            Debug.Assert(taskType != null);

            // Fast path: check if type is exactly Task first.
            if (taskType != typeof(Task))
            {
                Type innerTaskType = GetTaskInnerTypeOrNull(taskType);
                if (innerTaskType != null && typeof(Task).IsAssignableFrom(innerTaskType))
                {
                    throw new InvalidOperationException("Returning a nested Task is not supported. Did you mean to " +
                        "await or Unwrap the task instead of returning it?");
                }
            }
        }

        private static Type GetTaskInnerTypeOrNull(Type type)
        {
            Debug.Assert(type != null);
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                Type genericTypeDefinition = type.GetGenericTypeDefinition();

                if (typeof(Task<>) == genericTypeDefinition)
                {
                    return type.GetGenericArguments()[0];
                }
            }

            return null;
        }
    }
}
