// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Enumeration of possible states a <see cref="IConcurrencyThrottleProvider"/> can be in.
    /// </summary>
    public enum ThrottleState
    {
        /// <summary>
        /// The throttle provider doesn't have enough data yet to make a throttle determination.
        /// </summary>
        Unknown,

        /// <summary>
        /// The throttle is enabled.
        /// </summary>
        Enabled,

        /// <summary>
        /// The throttle is disabled.
        /// </summary>
        Disabled
    }
}
