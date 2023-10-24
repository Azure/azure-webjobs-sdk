// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Provider interface for returning a collection of <see cref="IScaleMonitor"/> instances.
    /// </summary>
    public interface IScaleMonitorCollectionProvider
    {
        /// <summary>
        /// Gets the collection of <see cref="IScaleMonitor"/> instances.
        /// </summary>
        /// <returns>The collection of <see cref="IScaleMonitor"/> instances.</returns>
        IEnumerable<IScaleMonitor> GetScaleMonitors();
    }
}