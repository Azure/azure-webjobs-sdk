// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs
{
    public interface IFunctionDataCache : IDisposable
    {
        /// <summary>
        /// Gets if the FunctionDataCache is enabled or not.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Put a given object (backed by shared memory) into the cache.
        /// </summary>
        /// <param name="cacheKey">Key corresponding to the object being inserted into the cache.</param>
        /// <param name="sharedMemoryMeta">Metadata about the shared memory region containing content of the object.</param>
        /// <param name="isIncrementActiveReference">If <see cref="true"/>, the reference counter for this object in the cache will be incremented (hence preventing it from being evicted).</param>
        /// <param name="isDeleteOnFailure">If <see cref="true"/>, in the case where the cache is unable to insert this object, the shared memory region containing the content of the object is removed.</param>
        /// <returns><see cref="true"/> if the object was added successfully, <see cref="false"/> otherwise.</returns>
        bool TryPut(FunctionDataCacheKey cacheKey, SharedMemoryMetadata sharedMemoryMeta, bool isIncrementActiveReference, bool isDeleteOnFailure);

        /// <summary>
        /// Get metadata about where the object corresponding to the given key is present.
        /// </summary>
        /// <param name="cacheKey">Key corresponding to the object being retrieved from the cache.</param>
        /// <param name="isIncrementActiveReference">If <see cref="true"/>, the reference counter for this object in the cache will be incremented (hence preventing it from being evicted).</param>
        /// <param name="sharedMemoryMeta">Metadata about the shared memory region containing content of the object.</param>
        /// <returns><see cref="true"/> if the object was found in the cache, <see cref="false"/> otherwise.</returns>
        bool TryGet(FunctionDataCacheKey cacheKey, bool isIncrementActiveReference, out SharedMemoryMetadata sharedMemoryMeta);

        /// <summary>
        /// Remove the object corresponding to the given key from the cache along with its backing shared memory region.
        /// Note: This will remove the object even if it has a non-zero reference counter; the caller must be sure when to delete it.
        /// </summary>
        /// <param name="cacheKey">Key corresponding to the object being removed from the cache.</param>
        /// <returns><see cref="true"/> if the object was removed from the cache, <see cref="false"/> otherwise.</returns>
        bool TryRemove(FunctionDataCacheKey cacheKey);

        /// <summary>
        /// Decrement the reference counter for the object corresponding to the given key by one.
        /// </summary>
        /// <param name="cacheKey">Key corresponding to the object whose reference counter is to be decremented in the cache.</param>
        void DecrementActiveReference(FunctionDataCacheKey cacheKey);
    }
}
