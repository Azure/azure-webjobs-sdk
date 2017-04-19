// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host.Lease
{
    /// <summary>
    /// Cause of lease failure
    /// </summary>
    public enum LeaseFailureReason
    {
        /// <summary>
        /// Conflict
        /// </summary>
        Conflict,

        /// <summary>
        /// Unknown failure
        /// </summary>
        Unknown
    }
}