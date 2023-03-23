// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Represents the aggregate scale status across all functions being monitored by the host.
    /// </summary>
    public class AggregateScaleStatus 
    {
        /// <summary>
        /// Gets or sets the aggregate scale vote.
        /// </summary>
        public ScaleVote Vote { get; set; }

        /// <summary>
        /// Gets or sets the aggregate target worker count.
        /// </summary>
        public int? TargetWorkerCount { get; set; }

        /// <summary>
        /// Gets or sets the individual <see cref="ScaleStatus"/>s for all functions being monitored.
        /// </summary>
        public IDictionary<string, ScaleStatus> FunctionScaleStatuses { get; set; }

        /// <summary>
        /// Gets or sets the individual <see cref="TargetScalerResult"/>s for all functions being monitored.
        /// </summary>
        public IDictionary<string, TargetScalerResult> FunctionTargetScalerResults { get; set; }
    }
}
