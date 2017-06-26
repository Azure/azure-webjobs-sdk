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
    public class FunctionExecutingContext : FunctionInvocationContext
    {
        /// <summary>
        /// Constructor to set the context
        /// </summary>
        /// <param name="id"></param>
        /// <param name="fullName"></param>
        /// <param name="arguments"></param>
        /// <param name="logger"></param>
        internal FunctionExecutingContext(Guid id, string fullName, IReadOnlyDictionary<string, object> arguments, ILogger logger) :
            base(id, fullName, arguments, logger)
        {
        }

        /// <summary>
        /// Constructor to set the context
        /// </summary>
        /// <param name="id"></param>
        /// <param name="fullName"></param>
        /// <param name="arguments"></param>
        /// <param name="logger"></param>
        /// <param name="methodInvoker"></param>
        internal FunctionExecutingContext(Guid id, string fullName, IReadOnlyDictionary<string, object> arguments, ILogger logger, IJobMethodInvoker methodInvoker) :
            base(id, fullName, arguments, logger, methodInvoker)
        {
        }
    }
}