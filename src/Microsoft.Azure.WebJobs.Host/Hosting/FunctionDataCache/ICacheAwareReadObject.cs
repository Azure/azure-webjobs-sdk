// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// An object that is being read as input.
    /// It may be read from storage if the cache does not have it. In this case, an attempt is made to cache this object for future reads.
    /// It may be read from the cache if it was previously read and cached.
    /// </summary>
    public interface ICacheAwareReadObject : IDisposable
    {
        /// <summary>
        /// Gets or sets if the object was a cache hit.
        /// If this is <see cref="true"/> (i.e. it was a cache it) then the <see cref="CacheObject"/> property must be used to
        /// read the object from the cache.
        /// Otherwise, the <see cref="BlobStream"/> property must be used to read it from storage.
        /// </summary>
        bool IsCacheHit { get; }

        /// <summary>
        /// Gets or sets the key corresponding to the object pointed to by this Stream.
        /// </summary>
        FunctionDataCacheKey CacheKey { get; }

        /// <summary>
        /// Gets or sets the metadata about the shared memory region containing content of the object pointed to by this Stream.
        /// </summary>
        SharedMemoryMetadata CacheObject { get; }

        /// <summary>
        /// Gets or sets the Stream pointing to this object in storage.
        /// </summary>
        Stream BlobStream { get; }

        /// <summary>
        /// Put the object into the cache.
        /// </summary>
        /// <param name="cacheObject">Object to put into the cache.</param>
        /// <param name="isIncrementActiveReference">Whether to keep an active reference on the object when inserted into the cache.
        /// This is needed in cases where this object needs to be read in the future and we don't want it to be evicted.
        /// e.g., when an object is being read as part of input binding and will need to be used right away, we don't want another binding to result in evicting this object.</param>
        /// <returns><see cref="true"/> if the object was successfully put into the cache, <see cref="false"/> otherwise.</returns>
        bool TryPutToCache(SharedMemoryMetadata cacheObject, bool isIncrementActiveReference);
    }
}
