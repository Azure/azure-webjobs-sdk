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
    /// Context class for IFunctionInvocationFilter.OnExecutingAsync <see cref="IFunctionInvocationFilter"/>>
    /// </summary>
    public class FunctionExecutingContext : FunctionInvocationContext
    {
        /// <summary>
        /// Constructor to set the context
        /// </summary>
        /// <param name="functionInstanceId"><see cref="FunctionInvocationContext"/></param>
        /// <param name="name"><see cref="FunctionInvocationContext"/></param>
        /// <param name="arguments"><see cref="FunctionInvocationContext"/></param>
        /// <param name="properties"><see cref="FunctionInvocationContext"/></param>
        /// <param name="logger"><see cref="FunctionInvocationContext"/></param>
        internal FunctionExecutingContext(Guid functionInstanceId, string name, Dictionary<string, object> arguments, Dictionary<string, object> properties, ILogger logger) :
            base(functionInstanceId, name, arguments, properties, logger)
        {
        }
    }
}