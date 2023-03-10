// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Represents aggregated scale status.
    /// </summary>
    public class AggregatedScaleStatus 
    {
        /// <summary>
        /// Gets or sets the current scale vote for all the triggers.
        /// </summary>
        public ScaleVote Vote { get; set; }

        /// <summary>
        /// Gets or sets the current target worker count for all the triggers.
        /// </summary>
        public int? TargetWorkerCount { get; set; }

        /// <summary>
        /// Gets or sets the incremental scale statuses for all the incremental triggera.
        /// </summary>
        public IDictionary<string, ScaleStatus> FunctionsScaleStatuses { get; set; }

        /// <summary>
        /// Gets or sets the target scale result for all the targeted triggers.
        /// </summary>
        public IDictionary<string, TargetScalerResult> FunctionsTargetScaleResults { get; set; }
    }
}
