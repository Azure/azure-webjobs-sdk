// InvokeFunctionAttribute.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// This is the first iteration of the InvokeFunctionAttribute
    /// </summary>
    [CLSCompliant(false)]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class InvokeFunctionFilterAttribute : InvocationFilterAttribute
    {
        /// <summary>
        /// When this attribute is used, take the name of the function to invoke
        /// </summary>
        /// <param name="functionName"></param>
        public InvokeFunctionFilterAttribute(string functionName)
        {
            FunctionName = functionName;
        }

        /// <summary>
        /// This is the function name to invoke
        /// </summary>
        public string FunctionName { get; }

        /// <summary>
        /// Call the requested function
        /// </summary>
        /// <returns></returns>
        public override Task OnExecutingAsync(FunctionExecutingContext executingContext, CancellationToken cancellationToken)
        {
            if (executingContext == null)
            {
                throw new ArgumentNullException("executingContext");
            }

            executingContext.Logger.LogInformation("Executing function: " + FunctionName);
            executingContext.Logger.LogInformation(executingContext.GetArguments().ToString());

            // TODO: Implement invoke function logic

            // object[] paramaters = executingContext.GetArguments();
            // JobHost host = executingContext.Host;
            // await host.CallAsync(FunctionName, cancellationToken);

            return Task.CompletedTask;
        }
    }
}
