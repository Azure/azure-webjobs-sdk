// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data;

namespace Microsoft.Azure.WebJobs.Host.Lease
{
    /// <summary>
    /// Lease info
    /// </summary>
    public class LeaseInformation
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public LeaseInformation(bool isLeaseAvailable, IDictionary<string, string> metadata)
        {
            this.IsLeaseAvailable = isLeaseAvailable;
            this.Metadata = metadata;
        }

        /// <summary>
        /// Specifies if the lease is available and can be acquired
        /// </summary>
        public bool IsLeaseAvailable { get; private set; }

        /// <summary>
        /// The lease metadata
        /// </summary>
        public IDictionary<string, string> Metadata { get; private set; }
    }
}
