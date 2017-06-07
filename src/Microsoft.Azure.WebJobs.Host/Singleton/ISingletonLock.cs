// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host
{
    interface ISingletonLock
    {
        string LockId { get; set; }

        // Task is signalled if the lease is lost (after it's been acquired) 
        Task LeaseLost { get; }
    }
}