// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Context class for <see cref="IFunctionInvocationFilter"/>>
    /// </summary>
    public class FunctionExecutedContext : FunctionInvocationContext
    {
        /// <summary>
        /// Constructor to set the context
        /// </summary>
        /// <param name="functionInstanceId">The function instance id</param>
        /// <param name="functionName">The function name</param>
        /// <param name="arguments">The arguments for the function</param>
        /// <param name="properties">User set properties</param>
        /// <param name="logger">The logger for logging information</param>
        /// <param name="result"></param>
        internal FunctionExecutedContext(Guid functionInstanceId, string functionName, Dictionary<string, object> arguments, Dictionary<string, object> properties, ILogger logger) :
            base(functionInstanceId, functionName, arguments, properties, logger)
        {
        }

        /// <summary>
        /// Gets or sets the function result
        /// </summary>
        public FunctionResult FunctionResult { get; set; }
    }
}