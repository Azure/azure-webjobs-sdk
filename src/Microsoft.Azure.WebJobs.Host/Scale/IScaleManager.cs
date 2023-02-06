// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Provides scale status.
    /// </summary>
    public interface IScaleManager
    {
        /// <summary>
        /// Gets the current scale status <see cref="ScaleStatus"> for all monitored functions.
        /// </summary>
        /// <param name="context">The <see cref="ScaleStatusContext"/>.</param>
        /// <returns>The current <see cref="ScaleStatus"/>.</returns>
        Task<ScaleStatus> GetScaleStatusAsync(ScaleStatusContext context);
    }
}
