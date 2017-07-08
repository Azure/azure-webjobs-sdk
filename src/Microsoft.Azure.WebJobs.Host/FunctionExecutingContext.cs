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
    /// The context describing a function that's about to be executed
    /// </summary>
    public class FunctionExecutingContext : FunctionInvocationContext
    {
        /// <summary>
        /// Constructor to set the context
        /// </summary>
        /// <param name="functionInstanceId"></param>
        /// <param name="name"></param>
        /// <param name="arguments"></param>
        /// <param name="logger"></param>
        internal FunctionExecutingContext(Guid functionInstanceId, string name, IReadOnlyDictionary<string, object> arguments, ILogger logger) :
            base(functionInstanceId, name, arguments, logger)
        {
        }
    }
}