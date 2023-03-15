// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Interface for providing a scale status
    /// </summary>
    public interface IScaleStatusProvider
    {
        /// <summary>
        /// Gets the scale status for all functions being monitored by the host.
        /// </summary>
        /// <param name="context">The <see cref="ScaleStatusContext"/>.</param>
        /// <returns>A task that returns the <see cref="AggregateScaleStatus"/>.</returns>
        Task<AggregateScaleStatus> GetScaleStatusAsync(ScaleStatusContext context);
    }
}
