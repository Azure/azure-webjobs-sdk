// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Context used by <see cref="ITargetScaler.GetScaleResultAsync(TargetScaleStatusContext)"/> to decide
    /// scale result.
    /// </summary>
    public class TargetScaleStatusContext
    {
        /// <summary>
        /// The current worker dyanimc worker concurrency. 
        /// </summary>
        /// <remarks>
        /// Value is reolved in the <see cref="ITargetScaler"/> implementation if null.
        /// </remarks>
        public int? InstanceConcurrency { get; set; }

        /// <summary>
        /// The current worker count for the host application.
        /// </summary>
        public int WorkerCount { get; set; }
    }
}
