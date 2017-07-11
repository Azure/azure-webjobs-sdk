// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Context class for IFunctionInvocationFilter.OnExecutedAsync <see cref="IFunctionInvocationFilter"/>>
    /// </summary>
    public class FunctionExecutedContext : FunctionInvocationContext
    {
        /// <summary>
        /// Constructor to set the context
        /// </summary>
        /// <param name="functionInstanceId"><see cref="FunctionInvocationContext"/></param>
        /// <param name="functionName"><see cref="FunctionInvocationContext"/></param>
        /// <param name="arguments"><see cref="FunctionInvocationContext"/></param>
        /// <param name="properties"><see cref="FunctionInvocationContext"/></param>
        /// <param name="logger"><see cref="FunctionInvocationContext"/>></param>
        /// <param name="result"></param>
        internal FunctionExecutedContext(Guid functionInstanceId, string functionName, Dictionary<string, object> arguments, Dictionary<string, object> properties, ILogger logger, FunctionResult result) :
            base(functionInstanceId, functionName, arguments, properties, logger)
        {
            Result = result;
        }

        /// <summary>
        /// Gets or sets the function result
        /// </summary>
        public FunctionResult Result { get; set; }
    }
}