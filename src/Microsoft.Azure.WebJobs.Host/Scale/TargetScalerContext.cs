// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Context used by <see cref="ITargetScaler.GetScaleResultAsync(TargetScalerContext)"/> to decide
    /// scale result.
    /// </summary>
    public class TargetScalerContext
    {
        /// <summary>
        /// The current concurrency for the target function.
        /// </summary>
        /// <remarks>
        /// When not specified, the scaler will determine the concurrency based on configuration.
        /// </remarks>
        public int? InstanceConcurrency { get; set; }
    }
}
