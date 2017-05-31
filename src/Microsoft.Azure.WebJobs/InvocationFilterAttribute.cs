// InvocationFilterAttribute.cs

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// This is the first iteration for the Invocation Filter Attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public abstract class InvocationFilterAttribute : Attribute
    {
        /// <summary>
        /// Tasks here should execute before the actual function is executed
        /// </summary>
        /// <param name="actionContext"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual Task OnExecutingAsync(FunctionExecutedContext actionContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Tasks here should execute after the actual function is executed
        /// </summary>
        /// <param name="actionExecutedContext"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual Task OnActionExecuted(FunctionExecutedContext actionExecutedContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}