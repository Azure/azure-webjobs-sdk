using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// This is the first iteration of the InvokeFunctionAttribute
    /// </summary>
    [CLSCompliant(false)]
    public class InvokeFunctionAttribute : InvocationFilterAttribute
    {
        /// <summary>
        /// This is the function name to invoke
        /// </summary>
        public string functionNameToInvoke;

        /// <summary>
        /// When this attribute is used, take the name of the function to invoke
        /// </summary>
        /// <param name="functionNameToInvoke"></param>
        public InvokeFunctionAttribute(string functionNameToInvoke)
        {
            this.functionNameToInvoke = functionNameToInvoke;
        }

        /// <summary>
        /// The function that actually invokes the seperate function
        /// </summary>
        /// <returns></returns>
        private Task invokeFunction()
        {
            return Task.CompletedTask;
        }
    }
}
