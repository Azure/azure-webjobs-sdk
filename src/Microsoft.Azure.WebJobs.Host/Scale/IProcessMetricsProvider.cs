// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// Type was moved from https://github.com/Azure/azure-functions-host/blob/dev/src/WebJobs.Script/Scale/IProcessMetricsProvider.cs

using System;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Provider for process level metrics.
    /// </summary>
    internal interface IProcessMetricsProvider
    {
        /// <summary>
        /// Gets the total processor time for the process.
        /// </summary>
        TimeSpan TotalProcessorTime { get; }

        /// <summary>
        /// Gets the amount of private memory currently used by the process.
        /// </summary>
        long PrivateMemoryBytes { get; }
    }
}
