// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host.Loggers
{
    /// <summary>
    /// Constant values for log categories.
    /// </summary>
    public static class LoggingCategories
    {
        /// <summary>
        /// The category for all logs written by the host.
        /// </summary>
        public const string Executor = "Host.Executor";

        /// <summary>
        /// The category for logs written by the function aggregator.
        /// </summary>
        public const string Aggregator = "Host.Aggregator";

        /// <summary>
        /// The category for logs written by the function executor.
        /// </summary>
        public const string Results = "Host.Results";

        /// <summary>
        /// The category for logs written from within user functions.
        /// </summary>
        public const string Function = "Function";
    }
}
