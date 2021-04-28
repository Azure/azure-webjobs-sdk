// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Host.Singleton
{
    public class KubernetesLockHandle : IDistributedLock
    {
        public string LockId { get; set; }

        public string OwnerId { get; set; }

        public string LockPeriod { get; set; }
    }
}
