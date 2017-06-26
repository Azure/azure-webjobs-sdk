// FunctionExecutedContext.cs

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// The context for an executed function. This needs to be expanded on later.
    /// </summary>
    [CLSCompliant(false)]
    public class FunctionExecutedContext : FunctionInvocationContext
    {
        /// <summary>
        /// Constructor to set the context
        /// </summary>
        /// <param name="id"></param>
        /// <param name="fullName"></param>
        /// <param name="arguments"></param>
        /// <param name="logger"></param>
        /// <param name="result"></param>
        internal FunctionExecutedContext(Guid id, string fullName, IReadOnlyDictionary<string, object> arguments, ILogger logger, FunctionResult result) :
            base(id, fullName, arguments, logger)
        {
            Result = result;
        }

        /// <summary>
        /// Constructor to set the context
        /// </summary>
        /// <param name="id"></param>
        /// <param name="fullName"></param>
        /// <param name="arguments"></param>
        /// <param name="logger"></param>
        /// <param name="result"></param>
        /// <param name="methodInvoker"></param>
        internal FunctionExecutedContext(Guid id, string fullName, IReadOnlyDictionary<string, object> arguments, ILogger logger, FunctionResult result, IJobMethodInvoker methodInvoker) :
            base(id, fullName, arguments, logger, methodInvoker)
        {
            Result = result;
            MethodInvoker = methodInvoker;
        }

        /// <summary>
        /// Make sure the final result is logged
        /// </summary>
        public FunctionResult Result { get; set; }
    }
}