// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Interface for monitoring lock renewals from the <see cref="SingletonAttribute"/>.
    /// </summary>
    public interface ISingletonRenewalMonitor
    {
        /// <summary>
        /// This method is called every time a singleton lock is renewed. It is up to the implementer to
        /// store this value and, during listener execution, determine if the lock is stale.
        /// </summary>
        /// <param name="renewalTime">The last renewal time.</param>
        /// <param name="lockPeriod">The lock period.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A Task.</returns>
        Task OnRenewalAsync(DateTime renewalTime, TimeSpan lockPeriod, CancellationToken cancellationToken);
    }
}