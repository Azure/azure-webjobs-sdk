// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Represents an aggregated throttle result.
    /// </summary>
    public class ConcurrencyThrottleAggregateStatus
    {
        public ConcurrencyThrottleAggregateStatus()
        {
            State = ThrottleState.Unknown;
        }

        /// <summary>
        /// Gets or sets current aggregate throttle state.
        /// </summary>
        public ThrottleState State { get; set; }

        /// <summary>
        /// Gets or sets the collection of currently enabled throttles.
        /// </summary>
        public ICollection<string>? EnabledThrottles { get; set; }

        /// <summary>
        /// Gets or sets the number of times the <see cref="State"/> has been in the current
        /// state consecutively.
        /// </summary>
        public int ConsecutiveCount { get; set; }
    }
}
