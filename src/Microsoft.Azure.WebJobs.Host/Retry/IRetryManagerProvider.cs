// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Interface defining a method used to create <see cref="IRetryManager"/>
    /// </summary>
    public interface IRetryManagerProvider
    {
        /// <summary>
        /// Creates a <see cref="IRetryManager"/> for execution with retries.
        /// </summary>
        /// <param name="retryAttribute">RetryAttribute</param>
        IRetryManager Create(RetryAttribute retryAttribute);
    }
}
