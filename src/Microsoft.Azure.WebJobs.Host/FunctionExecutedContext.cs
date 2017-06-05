// FunctionExecutedContext.cs

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// The context for an executed function. This needs to be expanded on later.
    /// </summary>
    [CLSCompliant(false)]
    public class FunctionExecutedContext
    {
        /// <summary>
        /// Constructor to set the context
        /// </summary>
        /// <param name="id"></param>
        /// <param name="fullName"></param>
        /// <param name="arguments"></param>
        /// <param name="logger"></param>
        public FunctionExecutedContext(Guid id, string fullName, object[] arguments, ILogger logger)
        {
            Id = id;
            FullName = fullName;
            Arguments = arguments;
            Logger = logger;
        }

        /// <summary>Gets or sets the ID of the function.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the fully qualified name of the function.</summary>
        public string FullName { get; set; }

        /// <summary>
        /// Gets or sets the parameters of the function
        /// </summary>
        public object[] Arguments { get; set; }
        
        /// <summary>
        /// Gets or sets the logger of the function
        /// </summary>
        public ILogger Logger { get; set; }
    }
}