// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// An invocation filter that invokes job methods
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public sealed class InvokeFunctionFilterAttribute : InvocationFilterAttribute
    {
        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="executingFilter">The name of the function to execute before the target function is called</param>
        /// <param name="executedFilter">The name of the function to execute after the target function is called</param>
        public InvokeFunctionFilterAttribute(string executingFilter = null, string executedFilter = null)
        {
            ExecutingFilter = executingFilter;
            ExecutedFilter = executedFilter;
        }

        /// <summary>
        /// The name of the function to execute before the target function is called
        /// </summary>
        public string ExecutingFilter { get; }

        /// <summary>
        /// The name of the function to execute after the target function is called
        /// </summary>
        public string ExecutedFilter { get; }

        /// <inheritdoc/>
        public override async Task OnExecutingAsync(FunctionExecutingContext executingContext, CancellationToken cancellationToken)
        {
            if (executingContext == null)
            {
                throw new ArgumentNullException(nameof(executingContext));
            }
            
            if (!string.IsNullOrEmpty(ExecutingFilter))
            {
                executingContext.Logger.LogInformation("Executing Function Filter '" + ExecutingFilter + "'");
                
                await InvokeJobFunctionAsync(ExecutingFilter, executingContext, cancellationToken);
            }
            else
            {
                await base.OnExecutingAsync(executingContext, cancellationToken);
            }
        }

        /// <inheritdoc/>
        public override async Task OnExecutedAsync(FunctionExecutedContext executedContext, CancellationToken cancellationToken)
        {
            if (executedContext == null)
            {
                throw new ArgumentNullException(nameof(executedContext));
            }

            if (!string.IsNullOrEmpty(ExecutedFilter))
            {
                executedContext.Logger.LogInformation("Executing Function Filter '" + ExecutedFilter + "'");
                
                await InvokeJobFunctionAsync(ExecutedFilter, executedContext, cancellationToken);
            }
            else
            {
                await base.OnExecutedAsync(executedContext, cancellationToken);
            }
        }

        internal static async Task InvokeJobFunctionAsync<TContext>(string methodName, TContext context, CancellationToken cancellationToken) where TContext : FunctionInvocationContext
        {
            MethodInfo methodInfo = null;

            foreach (var type in context.Config.TypeLocator.GetTypes())
            {
                if (methodName != null)
                {
                    methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                }

                if (methodInfo != null)
                {
                    break;
                }
            }

            if (methodInfo == null)
            {
                throw new InvalidOperationException("Incorrect method signature for invocation filter '{methodName}'.");
            }

            IDictionary<string, object> invokeArguments = new Dictionary<string, object>();
            string parameterName = methodInfo.GetParameters().SingleOrDefault(p => p.ParameterType.IsAssignableFrom(typeof(TContext))).Name;

            if (parameterName == null)
            {
                throw new InvalidOperationException("Incorrect method signature for invocation filter '{methodName}'.");
            }

            invokeArguments.Add(parameterName, context);

            await context.JobHost.CallAsync(methodInfo, invokeArguments, cancellationToken);
        }
    }
}
