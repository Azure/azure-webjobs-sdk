// FunctionExecutedContext.cs

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// The context for an executed function. This needs to be expanded on later.
    /// </summary>
    public class FunctionExecutedContext
    {
#pragma warning disable CS3001 // Argument type is not CLS-compliant
        /// <summary>
        /// Constructor to set the context
        /// </summary>
        /// <param name="id"></param>
        /// <param name="fullName"></param>
        /// <param name="parameters"></param>
        /// <param name="logger"></param>
        public FunctionExecutedContext(Guid id, string fullName, IEnumerable<ParameterDescriptor> parameters, ILogger logger)
#pragma warning restore CS3001 // Argument type is not CLS-compliant
        {
            Id = id;
            FullName = fullName;
            Parameters = parameters;
            Logger = logger;
        }

        /// <summary>Gets or sets the ID of the function.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the fully qualified name of the function.</summary>
        public string FullName { get; set; }

        /// <summary>
        /// Gets or sets the parameters of the function
        /// </summary>
        public IEnumerable<ParameterDescriptor> Parameters { get; set; }

#pragma warning disable CS3003 // Type is not CLS-compliant
        /// <summary>
        /// Gets or sets the logger of the function
        /// </summary>
        public ILogger Logger { get; set; }
#pragma warning restore CS3003 // Type is not CLS-compliant
    }
}