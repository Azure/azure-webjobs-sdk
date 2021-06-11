// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Provides aggregated access to all registered <see cref="IConcurrencyThrottleProvider"/> instances.
    /// </summary>
    public interface IConcurrencyThrottleManager
    {
        /// <summary>
        /// Gets a an aggregate throttle status by querying all registered <see cref="IConcurrencyThrottleProvider"/>.
        /// instances.
        /// </summary>
        /// <returns>The current <see cref="ConcurrencyThrottleAggregateStatus"/>.</returns>
        ConcurrencyThrottleAggregateStatus GetStatus();
    }
}
