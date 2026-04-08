// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Provides functionality for persisting target scaler errors across multiple host instances.
    /// When a target scaler throws <see cref="System.NotSupportedException"/>, the scaler identifier
    /// is recorded so all instances can fall back to incremental scale monitoring.
    /// </summary>
    public interface ITargetScalerErrorRepository
    {
        /// <summary>
        /// Adds a target scaler identifier to the set of scalers in error.
        /// </summary>
        /// <param name="scalerUniqueId">The unique identifier of the target scaler.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that completes when the write is finished.</returns>
        Task AddAsync(string scalerUniqueId, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the set of target scaler identifiers currently in error.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that returns the set of scaler identifiers in error.</returns>
        Task<ISet<string>> GetAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Clears all recorded target scaler errors, allowing scalers to be re-evaluated.
        /// Called on host startup so that fixes (e.g. granting Manage claim) take effect
        /// after an app restart.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that completes when the clear is finished.</returns>
        Task ClearAsync(CancellationToken cancellationToken);
    }
}
