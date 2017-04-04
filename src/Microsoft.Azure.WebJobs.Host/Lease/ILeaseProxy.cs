// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Lease;

namespace Microsoft.Azure.WebJobs.Host.Lease
{
    /// <summary>
    /// Interface for lease management
    /// </summary>
    public interface ILeaseProxy
    {
        /// <summary>
        /// Try acquiring lease.
        /// <returns>If successful, returns lease ID. Otherwise, returns null</returns>
        /// </summary>
        Task<string> TryAcquireLeaseAsync(LeaseDefinition leaseDefinition, CancellationToken cancellationToken);

        /// <summary>
        /// Acquire lease.
        /// <returns>If successful, returns lease ID. Otherwise, throws a <see cref="LeaseException"/> exception</returns>
        /// </summary>
        Task<string> AcquireLeaseAsync(LeaseDefinition leaseDefinition, CancellationToken cancellationToken);

        /// <summary>
        /// Renews lease.
        /// <returns>Throws a <see cref="LeaseException"/> exception if the operation fails</returns>
        /// </summary>
        Task RenewLeaseAsync(LeaseDefinition leaseDefinition, CancellationToken cancellationToken);

        /// <summary>
        /// Releases lease.
        /// <returns>Throws a <see cref="LeaseException"/> exception if the operation fails</returns>
        /// </summary>
        Task ReleaseLeaseAsync(LeaseDefinition leaseDefinition, CancellationToken cancellationToken);

        /// <summary>
        /// Writes lease metadata.
        /// <returns>Throws a <see cref="LeaseException"/> exception if the operation fails</returns>
        /// </summary>
        Task WriteLeaseMetadataAsync(LeaseDefinition leaseDefinition, string key, string value, CancellationToken cancellationToken);

        /// <summary>
        /// Reads information about the lease
        /// <returns>If successful, returns the lease information. Otherwise, it throws a <see cref="LeaseException"/> exception</returns>
        /// </summary>
        Task<LeaseInformation> ReadLeaseInfoAsync(LeaseDefinition leaseDefinition, CancellationToken cancellationToken);
    }
}
