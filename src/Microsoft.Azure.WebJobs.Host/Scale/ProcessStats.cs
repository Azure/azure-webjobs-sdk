// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// Type was moved from https://github.com/Azure/azure-functions-host/blob/dev/src/WebJobs.Script/Scale/ProcessStats.cs

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    internal class ProcessStats
    {
        /// <summary>
        /// Gets or sets the ID of the process these stats are for.
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Gets or sets the cpu load history collection. Each sample
        /// is a usage percentage.
        /// </summary>
        public IEnumerable<double> CpuLoadHistory { get; set; }

        /// <summary>
        /// Gets or sets the memory history collection. Each sample
        /// is in bytes.
        /// </summary>
        public IEnumerable<long> MemoryUsageHistory { get; set; }
    }
}
