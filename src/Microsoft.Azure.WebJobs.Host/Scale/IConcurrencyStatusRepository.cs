// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Provides functionality for <see cref="HostConcurrencySnapshot"/> persistence.
    /// </summary>
    public interface IConcurrencyStatusRepository
    {
        /// <summary>
        /// Writes the specified snapshot.
        /// </summary>
        /// <param name="snapshot">The snapshot to persist.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that completes when the write is finished.</returns>
        Task WriteAsync(HostConcurrencySnapshot snapshot, CancellationToken cancellationToken);

        /// <summary>
        /// Reads the last host concurrency snapshot if present.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that returns the snapshot if present, or null.</returns>
        Task<HostConcurrencySnapshot?> ReadAsync(CancellationToken cancellationToken);
    }
}
