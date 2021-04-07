// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs
{
    public class SharedMemoryMetadata
    {
        public SharedMemoryMetadata(string memoryMapName, long count)
        {
            MemoryMapName = memoryMapName;
            Count = count;
        }

        public string MemoryMapName { get; private set; }

        public long Count { get; private set; }
    }
}
