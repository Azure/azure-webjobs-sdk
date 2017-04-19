// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Host.Lease
{
    /// <summary>
    /// Lease definition
    /// </summary>
    public class LeaseDefinition
    {
        /// <summary>
        /// Account name associated with this lease
        /// </summary>
        public string AccountName { get; set; }

        /// <summary>
        /// List of nested logical namespaces that will contain the lease.
        /// Currently, the allowed number of namespaces is either 1 or 2.
        /// Implementations of <see cref="ILeaseProxy"/> can use this property in different ways.
        /// For example, a blob storage based lease implementation will map.
        /// Namespaces[0] to a container name and Namespaces[1] to a directory name in it.
        /// </summary>
        public IReadOnlyList<string> Namespaces { get; set; }

        /// <summary>
        /// The lease name.
        /// Implementations of <see cref="ILeaseProxy"/> can use this property in different ways.
        /// For example, a blob storage based lease implementation will map this to a blob name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The ID of a previously acquired lease
        /// </summary>
        public string LeaseId { get; set; }

        /// <summary>
        /// Duration of the lease
        /// </summary>
        public TimeSpan Period { get; set; }
    }
}
