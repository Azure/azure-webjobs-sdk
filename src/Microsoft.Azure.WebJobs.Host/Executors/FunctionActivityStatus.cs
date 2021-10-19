// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    /// <summary>
    /// Represents activity status the of job host
    /// </summary>
    public class FunctionActivityStatus
    {
        /// <summary>
        /// Gets or sets number of outstanding invocations
        /// </summary>
        public int OutstandingInvocations { get; set; }

        /// <summary>
        /// Gets or sets number of outstanding retries
        /// </summary>
        public int OutstandingRetries { get; set; }
    }
}
