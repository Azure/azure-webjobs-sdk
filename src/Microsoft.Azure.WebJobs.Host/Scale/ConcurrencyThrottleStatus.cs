// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Represents an throttle result.
    /// </summary>
    public class ConcurrencyThrottleStatus
    {
        /// <summary>
        /// Gets or sets current throttle state.
        /// </summary>
        public ThrottleState State { get; set; }

        /// <summary>
        /// Gets or sets the collection of currently enabled throttles.
        /// </summary>
        public ICollection<string>? EnabledThrottles { get; set; }
    }
}
