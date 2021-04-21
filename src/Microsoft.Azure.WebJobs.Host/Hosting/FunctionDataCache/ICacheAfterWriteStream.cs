// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs
{
    public interface ICacheAfterWriteStream
    {
        /// <summary>
        /// Put the object into the cache.
        /// </summary>
        /// <returns><see cref="true"/> if the object was successfully put into the cache, <see cref="false"/> otherwise.</returns>
        Task<bool> TryPutToFunctionDataCacheAsync();
    }
}
