// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// The context for an executed filter or function
    /// </summary>
    public abstract class FunctionInvocationContext
    {
        /// <summary>
        /// Constructor to set the context
        /// </summary>
        /// <param name="functionInstanceId">The function instance id</param>
        /// <param name="functionName">The function name</param>
        /// <param name="arguments">The arguments for the function</param>
        /// <param name="properties">User set properties</param>
        /// <param name="logger">The logger for logging information</param>
        protected FunctionInvocationContext(Guid functionInstanceId, string functionName, Dictionary<string, object> arguments, Dictionary<string, object> properties, ILogger logger)
        {
            FunctionInstanceId = functionInstanceId;
            FunctionName = functionName;
            Arguments = arguments;
            Properties = properties;
            Logger = logger;
        }

        /// <summary>Gets or sets the ID of the function.</summary>
        public Guid FunctionInstanceId { get; private set; }

        /// <summary>Gets or sets the name of the function.</summary>
        public string FunctionName { get; private set; }

        /// <summary>
        /// Gets or sets the function arguments
        /// </summary>
        public IReadOnlyDictionary<string, object> Arguments { get; private set; }

        /// <summary>
        /// User properties
        /// </summary>
        public IDictionary<string, object> Properties { get; private set; }

        /// <summary>
        /// Gets or sets the function logger
        /// </summary>
        public ILogger Logger { get; private set; }

        /// <summary>
        /// Gets or sets the JobHost
        /// </summary>
        internal JobHost JobHost { get; set; }

        /// <summary>
        /// Gets or sets the JobHostConfiguration
        /// </summary>
        internal JobHostConfiguration Config { get; set; }
    }
}