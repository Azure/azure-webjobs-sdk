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
    /// The context describing a function that's about to be executed after the main function
    /// </summary>
    [CLSCompliant(false)]
    public class FunctionExecutedContext : FunctionInvocationContext
    {
        /// <summary>
        /// Constructor to set the context
        /// </summary>
        /// <param name="functionInstanceId"></param>
        /// <param name="name"></param>
        /// <param name="arguments"></param>
        /// <param name="logger"></param>
        /// <param name="result"></param>
        internal FunctionExecutedContext(Guid functionInstanceId, string name, IReadOnlyDictionary<string, object> arguments, ILogger logger, FunctionResult result) :
            base(functionInstanceId, name, arguments, logger)
        {
            Result = result;
        }

        /// <summary>
        /// Gets or sets the function result
        /// </summary>
        public FunctionResult Result { get; set; }
    }
}