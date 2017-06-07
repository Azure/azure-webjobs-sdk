// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host
{
    internal interface ISingletonManager
    {
        // Attribute has scope, account, 
        // Returns null if not acquired. 
        Task<ISingletonLock> TryLockAsync(
            string lockId,  // which blob we try to acquire , incorporates SingleAttribute.Scope
            string lockOwnerId,  // for diagnostics, marker string for who owns the lock (functionInstanceId)
            SingletonAttribute attribute, // provides config (ignores scope) 
            CancellationToken cancellationToken);

        Task<string> GetLockOwnerAsync(
            SingletonAttribute attribute, 
            string lockId, 
            CancellationToken cancellationToken);

        // lockHandle is opaque object from TryLock
        Task ReleaseLockAsync(
            ISingletonLock lockHandle, 
            CancellationToken cancellationToken);
    }
}
