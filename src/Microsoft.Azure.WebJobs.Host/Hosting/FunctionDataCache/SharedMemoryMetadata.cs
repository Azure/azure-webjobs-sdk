// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Class describing a shared memory region.
    /// </summary>
    public class SharedMemoryMetadata
    {
        public SharedMemoryMetadata(string memoryMapName, long count)
        {
            MemoryMapName = memoryMapName;
            Count = count;
        }

        /// <summary>
        /// Name of the shared memory region.
        /// </summary>
        public string MemoryMapName { get; private set; }

        /// <summary>
        /// Number of bytes of content in the shared memory region.
        /// </summary>
        public long Count { get; private set; }
    }
}
