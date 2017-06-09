// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Handle for a lock returned by <see cref="IDistributedLockManager"/>
    /// </summary>
    public interface IDistributedLock
    {
        /// <summary>
        /// The Lock identity.  
        /// </summary>
        string LockId { get; }

        /// <summary>
        /// Task is set to completed if the lease is lost (after it's been acquired). 
        /// This allows consumers to get a notification if the lock failed to be renewed. 
        /// </summary>
        Task LeaseLost { get; }
    }
}