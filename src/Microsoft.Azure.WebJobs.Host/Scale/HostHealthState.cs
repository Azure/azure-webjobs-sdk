// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Enumeration of the possible host health status states.
    /// </summary>
    public enum HostHealthState
    {
        /// <summary>
        /// Not enough information to determine the state.
        /// </summary>
        Unknown,

        /// <summary>
        /// The host is under pressure and is currently overloaded.
        /// </summary>
        Overloaded,

        /// <summary>
        /// The host is currently in a healthy state.
        /// </summary>
        Ok
    }
}
