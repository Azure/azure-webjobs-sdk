// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Interface for providing scalers.
    /// </summary>
    public interface IScalerProvider
    {
        /// <summary>
        /// Gets a collection of <see cref="IScaleMonitor"/>s.
        /// </summary>
        /// <returns>The <see cref="IScaleMonitor"/>s.</returns>
        IEnumerable<IScaleMonitor> GetScaleMonitors();

        /// <summary>
        /// Gets a collection of <see cref="ITargetScaler"/>s.
        /// </summary>
        /// <returns>The <see cref="ITargetScaler"/>s.</returns>
        IEnumerable<ITargetScaler> GetTargetScalers();
    }
}
