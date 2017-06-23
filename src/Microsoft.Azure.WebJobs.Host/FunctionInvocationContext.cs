// FunctionContext.cs

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// The context for an executed function. This needs to be expanded on later.
    /// </summary>
    [CLSCompliant(false)]
    public abstract class FunctionInvocationContext
    { 
        /// <summary>
        /// Constructor to set the context
        /// </summary>
        /// <param name="id"></param>
        /// <param name="fullName"></param>
        /// <param name="arguments"></param>
        /// <param name="logger"></param>
        protected FunctionInvocationContext(Guid id, string fullName, IReadOnlyDictionary<string, object> arguments, ILogger logger)
        {
            Id = id;
            FullName = fullName;
            Arguments = arguments;
            Logger = logger;
        }

        /// <summary>
        /// Constructor to set the context with the JobHost
        /// </summary>
        /// <param name="id"></param>
        /// <param name="fullName"></param>
        /// <param name="arguments"></param>
        /// <param name="logger"></param>
        /// <param name="host"></param>
        protected FunctionInvocationContext(Guid id, string fullName, IReadOnlyDictionary<string, object> arguments, ILogger logger, JobHost host)
        {
            Id = id;
            FullName = fullName;
            Arguments = arguments;
            Logger = logger;
            Host = host;
        }

        /// <summary>Gets or sets the ID of the function.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the fully qualified name of the function.</summary>
        public string FullName { get; set; }
        
        /// <summary>
        /// Arguments from the function
        /// </summary>
        public IReadOnlyDictionary<string, object> Arguments { get; set; }

        /// <summary>
        /// Gets or sets the logger of the function
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// Gets or sets the host of the function
        /// </summary>
        internal JobHost Host { get; set; }
    }
}
