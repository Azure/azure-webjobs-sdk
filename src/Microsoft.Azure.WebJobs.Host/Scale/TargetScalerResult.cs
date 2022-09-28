// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Represents the scale result of a target scaler.
    /// </summary>
    public class TargetScalerResult
    {
        /// <summary>
        /// Gets or sets the target worker count.
        /// </summary>
        public int TargetWorkerCount { get; set; }
    }
}
