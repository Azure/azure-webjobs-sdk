// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    internal static class Invoker
    {
        public static IInvoker Create(MethodInfo method)
        {
            if (!method.IsStatic)
            {
                throw new NotSupportedException("Only static methods can be invoked.");
            }

            Type returnType = method.ReturnType;

            if (returnType != typeof(void) && returnType != typeof(Task))
            {
                throw new NotSupportedException("Methods may only return void or Task.");
            }

            // Parameters to invoker
            ParameterExpression parametersParameter = Expression.Parameter(typeof(object[]), "parameters");

            List<Expression> callArguments = new List<Expression>();
            ParameterInfo[] parameterInfos = method.GetParameters();
            Debug.Assert(parameterInfos != null);

            for (int index = 0; index < parameterInfos.Length; index++)
            {
                ParameterInfo parameterInfo = parameterInfos[index];
                BinaryExpression indexedParameter = Expression.ArrayIndex(parametersParameter,
                    Expression.Constant(index));
                UnaryExpression callArgument = Expression.Convert(indexedParameter, parameterInfo.ParameterType);

                // callArgument is "(T) parameters[index]"
                callArguments.Add(callArgument);
            }

            MethodCallExpression call = Expression.Call(null, method, callArguments);

            if (call.Type == typeof(void))
            {
                // for: public void JobMethod()
                Expression<Action<object[]>> lambda = Expression.Lambda<Action<object[]>>(call, parametersParameter);
                Action<object[]> compiled = lambda.Compile();
                return new VoidInvoker(compiled);
            }
            else
            {
                // for: public Task JobMethod()
                Debug.Assert(call.Type == typeof(Task));
                Expression<Func<object[], Task>> lambda = Expression.Lambda<Func<object[], Task>>(call,
                    parametersParameter);
                Func<object[], Task> compiled = lambda.Compile();
                return new TaskInvoker(compiled);
            }
        }
    }
}
