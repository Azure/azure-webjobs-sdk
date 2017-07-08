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
    public sealed class InvokeFunctionFilterAttribute : InvocationFilterAttribute, IFunctionInvocationFilter
    {
        /// <summary>
        /// When this attribute is used, take the name of the function to invoke
        /// </summary>
        /// <param name="executingFilter"></param>
        /// <param name="executedFilter"></param>
        public InvokeFunctionFilterAttribute(string executingFilter = null, string executedFilter = null)
        {
            ExecutingFilter = executingFilter;
            ExecutedFilter = executedFilter;
        }

        /// <summary>
        /// Executing filter
        /// </summary>
        public string ExecutingFilter { get; }

        /// <summary>
        /// Executed filter
        /// </summary>
        public string ExecutedFilter { get; }

        /// <summary>
        /// Call the requested job method before the main function call
        /// </summary>
        /// <returns></returns>
        public override async Task OnExecutingAsync(FunctionExecutingContext executingContext, CancellationToken cancellationToken)
        {
            if (executingContext == null)
            {
                throw new ArgumentNullException("executingContext");
            }
            
            if (!string.IsNullOrEmpty(ExecutingFilter))
            {
                executingContext.Logger.LogInformation("Executing Function Filter '" + ExecutingFilter + "'");

                try
                {
                    await InvokeJobFunctionAsync(ExecutingFilter, executingContext, cancellationToken);
                }
                catch (Exception e)
                {
                    executingContext.Logger.LogInformation(e.ToDetails());
                }
            }
            else
            {
                await base.OnExecutingAsync(executingContext, cancellationToken);
            }
        }

        /// <summary>
        /// Call the requested job method after the main function call
        /// </summary>
        /// <returns></returns>
        public override async Task OnExecutedAsync(FunctionExecutedContext executedContext, CancellationToken cancellationToken)
        {
            if (executedContext == null)
            {
                throw new ArgumentNullException("executingContext");
            }

            if (!string.IsNullOrEmpty(ExecutedFilter))
            {
                executedContext.Logger.LogInformation("Executing Function Filter '" + ExecutedFilter + "'");

                try
                {
                    await InvokeJobFunctionAsync(ExecutedFilter, executedContext, cancellationToken);
                }
                catch (Exception e)
                {
                    executedContext.Logger.LogInformation(e.ToDetails());
                }
            }
            else
            {
                await base.OnExecutedAsync(executedContext, cancellationToken);
            }
        }

        internal async Task InvokeJobFunctionAsync<TContext>(string methodName, TContext context, CancellationToken cancellationToken) where TContext : FunctionInvocationContext
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

            // If it is null, this means that we need to look for the methodinfo from the class
            // TODO: Implement class level methodinfo onExecuting/onExecuted invocation

            IDictionary<string, object> invokeArguments = new Dictionary<string, object>();
            string parameterName = methodInfo.GetParameters().SingleOrDefault(p => p.ParameterType == typeof(TContext)).Name;
            invokeArguments.Add(parameterName, context);

            await context.JobHost.CallAsync(methodInfo, invokeArguments, cancellationToken);
        }
    }
}
