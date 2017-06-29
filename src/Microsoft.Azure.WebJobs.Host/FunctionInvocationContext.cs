// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// The context describing a function that's about to be executed
    /// </summary>
    [CLSCompliant(false)]
    public abstract class FunctionInvocationContext
    { 
        /// <summary>
        /// Constructor to set the context
        /// </summary>
        /// <param name="functionInstanceId"></param>
        /// <param name="name"></param>
        /// <param name="arguments"></param>
        /// <param name="logger"></param>
        internal FunctionInvocationContext(Guid functionInstanceId, string name, IReadOnlyDictionary<string, object> arguments, ILogger logger)
        {
            FunctionInstanceId = functionInstanceId;
            Name = name;
            Arguments = arguments;
            Logger = logger;
        }

        /// <summary>Gets or sets the ID of the function instance.</summary>
        public Guid FunctionInstanceId { get; }

        /// <summary>Gets or sets the name of the function.</summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Arguments from the function
        /// </summary>
        public IReadOnlyDictionary<string, object> Arguments { get; set; }

        /// <summary>
        /// User properties
        /// </summary>
        public IDictionary<string, object> Properties { get;  }

        /// <summary>
        /// Gets or sets the logger of the function
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// Gets or sets the JobHost
        /// </summary>
        internal JobHost JobHost { get; set; }

        /// <summary>
        /// Gets or sets the configuration for the JobHost
        /// </summary>
        internal JobHostConfiguration Config { get; set; }
    }
}
