// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Interface defining an Azure Functions scaler that makes scale decisions based on current 
    /// event source metrics and function concurrency.
    /// </summary>
    public interface ITargetScaler
    {
        /// <summary>
        /// Returns the <see cref="TargetScalerDescriptor"/> for this target scaler.
        /// </summary>
        TargetScalerDescriptor TargetScalerDescriptor { get; }

        /// <summary>
        /// Return the current scale result based on the specified context.
        /// </summary>
        /// <param name="context">The <see cref="TargetScalerContext"/> to use to determine
        /// the scale result.</param>
        /// <returns>The scale result.</returns>
        Task<TargetScalerResult> GetScaleResultAsync(TargetScalerContext context);
    }
}
