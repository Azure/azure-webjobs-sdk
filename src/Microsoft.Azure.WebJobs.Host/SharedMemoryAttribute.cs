// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Description
{
    /// <summary>
    /// TODO .
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    [Binding]
    public sealed class SharedMemoryAttribute : Attribute
    {
        private string _memoryMapName;
        private long _count;

        /// <summary>
        /// TODO .
        /// </summary>
        /// <param name="memoryMapName"></param>
        /// <param name="count"></param>
        public SharedMemoryAttribute(string memoryMapName, long count)
        {
            _memoryMapName = memoryMapName;
            _count = count;
        }

        /// <summary>
        /// Gets the name of the shared memory map where the value is present.
        /// </summary>
        public string MemoryMapName
        {
            get { return _memoryMapName; }
        }

        /// <summary>
        /// Number of bytes of data.
        /// </summary>
        public long Count
        {
            get { return _count; }
        }
    }
}
