// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Describes the scope at which <see cref="IScaleMonitor"/> runs.
    /// </summary>
    public enum ScaleMonitorScope
    {
        /// <summary>
        /// The monitor is scoped at the application level. A single monitor runs for all worker instances.
        /// </summary>
        Application = 0,

        /// <summary>
        /// The monitor is scoped at the instance level. The monitor runs on each worker instance.
        /// </summary>
        Instance
    }
}
