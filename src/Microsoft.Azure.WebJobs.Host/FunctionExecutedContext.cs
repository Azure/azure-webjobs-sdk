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
    /// The context for an executed function
    /// </summary>
    [CLSCompliant(false)]
    public class FunctionExecutedContext : FunctionInvocationContext
    {
        /// <summary>
        /// Constructor to set the context
        /// </summary>
        /// <param name="id"></param>
        /// <param name="fullName"></param>
        /// <param name="arguments"></param>
        /// <param name="logger"></param>
        /// <param name="result"></param>
        internal FunctionExecutedContext(Guid id, string fullName, IReadOnlyDictionary<string, object> arguments, ILogger logger, FunctionResult result) :
            base(id, fullName, arguments, logger)
        {
            Result = result;
        }

        /// <summary>
        /// Make sure the final result is logged
        /// </summary>
        public FunctionResult Result { get; set; }
    }
}