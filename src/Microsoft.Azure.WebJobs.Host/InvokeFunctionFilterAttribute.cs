// InvokeFunctionAttribute.cs

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
            
            // TODO: Implement invoke function logic
            if (!string.IsNullOrEmpty(ExecutingFilter))
            {
                IReadOnlyDictionary<string, object> paramaters = executingContext.Arguments;
                JobHost host = executingContext.Host;
                MethodInfo methodInfo = null;

                foreach (var type in host.Configuration.TypeLocator.GetTypes())
                {
                    methodInfo = type.GetMethods().SingleOrDefault(p => string.Compare(p.Name, ExecutingFilter, StringComparison.OrdinalIgnoreCase) == 0);
                    // methodInfo = type.GetMethod(ExecutingFilter, new Type[] { typeof(FunctionExecutingContext) });
                }

                if (methodInfo != null)
                {
                    executingContext.Logger.LogInformation("Executing function from filter...");

                    var parameterName = methodInfo.GetParameters()[0].Name;
                    Dictionary<string, object> parameters = new Dictionary<string, object>();
                    parameters.Add(parameterName, executingContext);

                    try
                    {
                        await host.CallAsync(methodInfo, parameters, cancellationToken);
                    }
                    catch
                    {
                    }

                    // function executed
                    executingContext.Logger.LogInformation("Executed function: " + ExecutingFilter);
                }
                else
                {
                    // TODO: @Hamza add error handling
                }
            }
            else
            {
                await base.OnExecutingAsync(executingContext, cancellationToken);
            }
        }
    }
}
