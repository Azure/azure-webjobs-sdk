// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Class representing an object that is present in the cache and can be read from there.
    /// This is handed out by the azure-sdk-for-net storage extension when the object it is trying to read (e.g. Blob)
    /// is already present in the <see cref="IFunctionDataCache"/>.
    /// The caller can then, instead of using this Stream to read it, use the <see cref="SharedMemoryMetadata"/> property
    /// of this class to read the object content from shared memory.
    /// It is used by azure-functions-host in order to read an object from the cache before invoking a function.
    /// Once done, this Stream will be destroyed and it will decrement the active reference counter in the cache so that
    /// the object can be appropriately removed by the <see cref="IFunctionDataCache"/>.
    /// </summary>
    public class FunctionDataCacheStream : Stream
    {
        // Cache where the object can be read from.
        private readonly IFunctionDataCache _functionDataCache;

        // Indicates if this Stream has been disposed.
        private bool _isDisposed;

        public FunctionDataCacheStream(FunctionDataCacheKey cacheKey, SharedMemoryMetadata sharedMemoryMeta, IFunctionDataCache functionDataCache)
        {
            CacheKey = cacheKey;
            SharedMemoryMetadata = sharedMemoryMeta;
            _functionDataCache = functionDataCache;
            _isDisposed = false;
        }

        /// <summary>
        /// Gets or sets the metadata about the shared memory region containing content of the object pointed to by this Stream.
        /// </summary>
        public SharedMemoryMetadata SharedMemoryMetadata { get; private set; }

        /// <summary>
        /// Gets or sets the key corresponding to the object pointed to by this Stream.
        /// </summary>
        public FunctionDataCacheKey CacheKey { get; private set; }

        public override bool CanRead => throw new System.NotImplementedException();

        public override bool CanSeek => throw new System.NotImplementedException();

        public override bool CanWrite => throw new System.NotImplementedException();

        public override long Length => throw new System.NotImplementedException();

        public override long Position { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public override void Flush()
        {
            throw new System.NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new System.NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new System.NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new System.NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Decreases the ref-count of this object in the <see cref="IFunctionDataCache"/>.
        /// </summary>
        /// <param name="isDisposing"></param>
        protected override void Dispose(bool isDisposing)
        {
            if (!_isDisposed)
            {
                _functionDataCache.DecrementActiveReference(CacheKey);
                _isDisposed = true;
            }
        }
    }
}