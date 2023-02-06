﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Represents the current scale status for an <see cref="IScaleMonitor"/>.
    /// </summary>
    public class ScaleStatus
    {
        /// <summary>
        /// Gets or sets the current scale decision.
        /// </summary>
        public ScaleVote Vote { get; set; }

        /// <summary>
        /// Gets or sets the current target worker count.
        /// </summary>
        public int? TargetWorkerCount { get; set; }

        /// <summary>
        /// Gets the individual scale statuses for all monitored functions.
        /// </summary>
        public IDictionary<string, ScaleStatus> FunctionScaleStatuses { get; set; }
    }
}
