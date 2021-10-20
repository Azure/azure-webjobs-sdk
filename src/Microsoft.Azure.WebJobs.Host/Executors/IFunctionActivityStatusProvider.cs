// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    /// <summary>
    /// Interface providing access to job function activity information for the job host.
    /// </summary>
    public interface IFunctionActivityStatusProvider
    {
        /// <summary>
        /// Gets the current <see cref="FunctionActivityStatus"/> of the job host.
        /// </summary>
        /// <returns></returns>
        public FunctionActivityStatus GetStatus();
    }
}
