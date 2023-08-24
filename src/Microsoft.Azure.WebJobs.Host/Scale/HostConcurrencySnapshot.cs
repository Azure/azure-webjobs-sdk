// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Represents a snapshot of the current concurrency status of the host.
    /// </summary>
    public class HostConcurrencySnapshot
    {
        /// <summary>
        /// Gets or sets the timestamp the snapshot was taken.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the number of cores the host machine has.
        /// </summary>
        public int NumberOfCores { get; set; }

        /// <summary>
        /// Gets the collection of current function concurrency snapshots, indexed
        /// by function ID.
        /// </summary>
        public Dictionary<string, FunctionConcurrencySnapshot> FunctionSnapshots { get; set; }

        public override bool Equals(object obj) => Equals(obj as HostConcurrencySnapshot);

        private bool Equals(HostConcurrencySnapshot other)
        {
            if (other == null)
            {
                return false;
            }

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (NumberOfCores != other.NumberOfCores)
            {
                return false;
            }

            if ((FunctionSnapshots == null && other.FunctionSnapshots != null && other.FunctionSnapshots.Count > 0) ||
                (FunctionSnapshots != null && FunctionSnapshots.Count > 0 && other.FunctionSnapshots == null))
            {
                return false;
            }

            if (FunctionSnapshots != null && other.FunctionSnapshots != null)
            {
                if (FunctionSnapshots.Count() != other.FunctionSnapshots.Count() ||
                    FunctionSnapshots.Keys.Union(other.FunctionSnapshots.Keys, StringComparer.OrdinalIgnoreCase).Count() != FunctionSnapshots.Count())
                {
                    // function set aren't equal
                    return false;
                }

                // we know we have the same set of functions in both 
                // we now want to compare each function snapshot
                foreach (var functionId in FunctionSnapshots.Keys)
                {
                    if (other.FunctionSnapshots.TryGetValue(functionId, out FunctionConcurrencySnapshot otherFunctionSnapshot) &&
                        FunctionSnapshots.TryGetValue(functionId, out FunctionConcurrencySnapshot functionSnapshot) &&
                        !functionSnapshot.Equals(otherFunctionSnapshot))
                    {
                        return false;
                    }
                }
            }
            
            // if none of the above checks have returned false, the snapshots
            // are equal
            return true;
        }

        public override int GetHashCode()
        {
            int hashCode = NumberOfCores.GetHashCode();

            if (FunctionSnapshots != null)
            {
                foreach (var functionSnapshot in FunctionSnapshots)
                {
                    hashCode |= functionSnapshot.Key.GetHashCode() ^ functionSnapshot.Value.GetHashCode();
                }
            }

            return hashCode;
        }
    }
}
