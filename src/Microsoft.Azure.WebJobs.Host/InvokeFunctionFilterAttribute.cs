// InvokeFunctionAttribute.cs

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// This is the first iteration of the InvokeFunctionAttribute
    /// </summary>
    [CLSCompliant(false)]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public sealed class InvokeFunctionFilterAttribute : InvocationFilterAttribute
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
        /// Call the requested function
        /// </summary>
        /// <returns></returns>
        public override async Task OnExecutingAsync(FunctionExecutingContext executingContext, CancellationToken cancellationToken)
        {
            if (executingContext == null)
            {
                throw new ArgumentNullException("executingContext");
            }

            executingContext.Logger.LogInformation("Executing function from filter...");

            Dictionary<string, object> invokeArguments = new Dictionary<string, object>();
            invokeArguments.Add(executingContext.ToString(), executingContext);

            if (!string.IsNullOrEmpty(ExecutingFilter))
            {
                try
                {
                    await executingContext.MethodInvoker.InvokeAsync(ExecutingFilter, invokeArguments, cancellationToken);
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
        /// Call the requested function
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
                try
                {
                    await executedContext.MethodInvoker.InvokeAsync(ExecutedFilter, executedContext.Arguments, cancellationToken);
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
    }
}
