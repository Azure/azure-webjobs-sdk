// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Represents the health status of the host process.
    /// </summary>
    public class HostProcessStatus
    {
        /// <summary>
        /// Gets the current health status of the host.
        /// </summary>
        public HostHealthState State { get; set; }

        /// <summary>
        /// Gets the collection of currently exceeded limits.
        /// </summary>
        public ICollection<string> ExceededLimits { get; set; }
    }
}
