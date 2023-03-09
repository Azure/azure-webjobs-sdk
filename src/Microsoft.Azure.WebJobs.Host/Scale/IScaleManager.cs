// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Provides scale status.
    /// </summary>
    public interface IScaleManager
    {
        /// <summary>
        /// Gets the current scale status <see cref="AggregatedScaleStatus">.
        /// </summary>
        /// <param name="context">The <see cref="ScaleStatusContext"/>.</param>
        /// <returns>The current <see cref="ScaleStatus"/>.</returns>
        Task<AggregatedScaleStatus> GetAggregatedScaleStatusAsync(ScaleStatusContext context);
    }
}
