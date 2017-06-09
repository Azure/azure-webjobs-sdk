// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Manage distributed locks. A lock is specified by (account, lockId). 
    /// </summary>
    /// <remarks>
    /// The default implementation of this is based on blob leases. 
    /// 1. Account can be null or it may be a storage account name intended for <see cref="IStorageAccountProvider"/>. 
    /// 2. lockId has the same naming restrictions as blobs. 
    /// 3. For distributed locks, this implementation is responsible for renewing the locks and setting <see cref="IDistributedLock.LeaseLost"/> if the lock can't be renewed. 
    /// </remarks>    
    public interface IDistributedLockManager
    {
        /// <summary>
        /// Try to acquire a lock specified by (account, lockId).                 
        /// </summary>
        /// <param name="account">a string specifying the account to use. LockIds are scoped to an account </param>
        /// <param name="lockId">the name of the lock. </param>
        /// <param name="lockOwnerId">a string hint specifying who owns this lock. </param>
        /// <param name="lockPeriod">As this period nears expiry, the lock will be automatically renewed.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>null if can't acquire the lock. This is common if somebody else holds it.</returns>
        Task<IDistributedLock> TryLockAsync(
            string account,
            string lockId, 
            string lockOwnerId,
            TimeSpan lockPeriod,  
            CancellationToken cancellationToken);

        /// <summary>
        /// Called by the client to renew the lease. The timing internals here are determined by <see cref="SingletonConfiguration"/>
        /// </summary>
        /// <param name="lockHandle"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>A hint used for determining the next time delay in calling Renew.  </returns>
        /// <remarks>
        /// If this throws an exception, the lease is cancelled. 
        /// </remarks>
        Task<bool> RenewAsync(IDistributedLock lockHandle, CancellationToken cancellationToken);

        /// <summary>
        /// Get the owner for a given lock or null if not held. 
        /// This is used for diagnostics. The lock owner can change immediately after this function returned, so callers 
        /// can't be guaranteed the owner still the same. 
        /// </summary>
        /// <param name="account"></param>
        /// <param name="lockId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<string> GetLockOwnerAsync(
            string account, 
            string lockId, 
            CancellationToken cancellationToken);

        /// <summary>
        /// Release a lock that was previously acquired via TryLockAsync.
        /// </summary>
        /// <param name="lockHandle"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task ReleaseLockAsync(
            IDistributedLock lockHandle, 
            CancellationToken cancellationToken);
    }
}
