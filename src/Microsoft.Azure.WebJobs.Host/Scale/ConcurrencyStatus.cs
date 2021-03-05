// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    public class ConcurrencyStatus
    {
        private object _syncLock = new object();

        public ConcurrencyStatus()
        {
            FetchCount = 0;
            CurrentParallelism = 1;
            OutstandingInvocations = 0;
            LastAdjustmentTimestamp = DateTime.UtcNow;
            ExecutionsSinceLastConcurrencyAdjustment = 0;
        }

        public bool ThrottleEnabled { get; set; }

        public int FetchCount { get; set; }

        public int CurrentParallelism { get; set; }

        public int OutstandingInvocations { get; set; }

        internal DateTime LastAdjustmentTimestamp { get; set; }

        internal int LastFailedConcurrencyLevel { get; set; }

        internal DateTime? LastFailedAdjustmentTimestamp { get; set; }

        internal int ExecutionsSinceLastConcurrencyAdjustment { get; set; }

        internal int ConsecutiveHealthyCount { get; set; }

        internal int ConsecutiveUnhealthyCount { get; set; }

        public void FunctionStarted()
        {
            lock (_syncLock)
            {
                OutstandingInvocations++;
                ExecutionsSinceLastConcurrencyAdjustment++;
            }
        }

        public void FunctionCompleted()
        {
            lock (_syncLock)
            {
                OutstandingInvocations--;
            }
        }
    }
}
