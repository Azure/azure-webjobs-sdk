// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// TODO.
    /// </summary>
    public interface ICacheAwareWriteObject
    {
        /// <summary>
        /// Gets or sets the Stream pointing to this object in storage.
        /// </summary>
        Stream BlobStream { get; }

        /// <summary>
        /// Put the object into the cache.
        /// </summary>
        /// <param name="isDeleteOnFailure">If <see cref="true"/>, in the case where the cache is unable to insert this object, the local resources pointed to by the Stream (which were to be cached) will be deleted.</param>
        /// <returns><see cref="true"/> if the object was successfully put into the cache, <see cref="false"/> otherwise.</returns>
        Task<bool> TryPutToCacheAsync(bool isDeleteOnFailure);
    }
}
