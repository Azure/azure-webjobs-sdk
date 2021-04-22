// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Class representing an object that can be cached.
    /// This is to identify to the receiver of the Stream that the content of this Stream can be cached
    /// using the key corresponding to the object pointed by this Stream.
    /// The typical usage pattern for this is: after reading the content of this Stream into shared memory,
    /// put that content into the cache.
    /// If the content was put into the cache with an active reference counter increment, the reference counter
    /// will be decremented when this Stream is Disposed.
    /// It is used by azure-functions-host; the azure-sdk-for-net storage extension will create this Stream when
    /// indicating that the object (e.g. Blob) can be cached. Azure-functions-host will then read the content into
    /// shared memory and use the <see cref="CacheKey"/> property of this Stream to add the object into the cache with
    /// an active reference count. The object will then be read by a functions worker for execution of an invocation.
    /// Once done, this Stream will be destroyed and it will decrement the active reference counter in the cache so that
    /// the object can be appropriately removed by the <see cref="IFunctionDataCache"/>.
    /// </summary>
    public class CacheableObjectStream : Stream
    {
        // Inner Stream pointing to an object in remote storage (e.g. Blob).
        private readonly Stream _inner;

        // Cache where the object can be inserted into.
        private readonly IFunctionDataCache _functionDataCache;

        // Indicates if this Stream has been disposed.
        private bool _isDisposed;

        private bool _decrementRefCountOnDispose;

        public CacheableObjectStream(FunctionDataCacheKey cacheKey, Stream innerStream, IFunctionDataCache functionDataCache)
        {
            CacheKey = cacheKey;
            _inner = innerStream;
            _functionDataCache = functionDataCache;
            _isDisposed = false;
            _decrementRefCountOnDispose = false;
        }

        /// <summary>
        /// Gets or sets the key corresponding to the object pointed to by this Stream.
        /// </summary>
        public FunctionDataCacheKey CacheKey { get; private set; }

        public override bool CanRead
        {
            get
            {
                return _inner.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return _inner.CanSeek;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return _inner.CanWrite;
            }
        }

        public override long Length
        {
            get
            {
                return _inner.Length;
            }
        }

        public override long Position
        {
            get
            {
                return _inner.Position;
            }

            set
            {
                _inner.Position = value;
            }
        }

        public override void Flush()
        {
            _inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Write(buffer, offset, count);
        }

        public bool TryCacheObject(SharedMemoryMetadata sharedMemoryMeta)
        {
            if (!_functionDataCache.TryPut(CacheKey, sharedMemoryMeta, isIncrementActiveReference: true, isDeleteOnFailure: false))
            {
                return false;
            }

            _decrementRefCountOnDispose = true;
            return true;
        }

        /// <summary>
        /// Decreases the ref-count of this object in the <see cref="IFunctionDataCache"/>.
        /// </summary>
        /// <param name="isDisposing"></param>
        protected override void Dispose(bool isDisposing)
        {
            if (!_isDisposed)
            {
                if (_decrementRefCountOnDispose)
                {
                    _functionDataCache.DecrementActiveReference(CacheKey);
                }

                _isDisposed = true;
            }
        }
    }
}
