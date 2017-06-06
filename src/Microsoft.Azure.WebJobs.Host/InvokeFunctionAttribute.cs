// InvokeFunctionAttribute.cs

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// This is the first iteration of the InvokeFunctionAttribute
    /// </summary>
    [CLSCompliant(false)]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class InvokeFunctionAttribute : InvocationFilterAttribute
    {
        /// <summary>
        /// When this attribute is used, take the name of the function to invoke
        /// </summary>
        /// <param name="functionNameToInvoke"></param>
        public InvokeFunctionAttribute(string functionNameToInvoke)
        {
            FunctionNameToInvoke = functionNameToInvoke;
        }

        /// <summary>
        /// This is the function name to invoke
        /// </summary>
        public string FunctionNameToInvoke { get; }

        /// <summary>
        /// Call the requested function
        /// </summary>
        /// <returns></returns>
        public override Task OnPreFunctionInvocation(FunctionExecutingContext executingContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
