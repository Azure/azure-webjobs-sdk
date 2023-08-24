// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Defines an interface for providing throttle signals to <see cref="ConcurrencyManager"/> to allow
    /// concurrency to be dynamically adjusted at runtime based on throttle state.
    /// </summary>
    public interface IConcurrencyThrottleProvider
    {
        /// <summary>
        /// Returns the current throttle status for this provider.
        /// </summary>
        /// <param name="logger">Optional logger to write throttle status to.</param>
        /// <returns>The current <see cref="ConcurrencyThrottleStatus"/>.</returns>
        ConcurrencyThrottleStatus GetStatus(ILogger? logger = null);
    }
}
