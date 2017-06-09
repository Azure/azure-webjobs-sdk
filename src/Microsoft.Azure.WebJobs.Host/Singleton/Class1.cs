// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Timers;

namespace Microsoft.Azure.WebJobs.Host
{
    // $$$ Handle from SingletonManager
    internal class LockWrapper
    {
        public LockWrapper(IDistributedLock handle, ITaskSeriesTimer renewal)
        {
            this.InnerLock = handle;
            this.LeaseRenewalTimer = renewal;
        }

        public IDistributedLock InnerLock { get; private set; }
        public ITaskSeriesTimer LeaseRenewalTimer { get; private set; }
    }
}
