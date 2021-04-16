// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// TODO
    /// </summary>
    public class FunctionDataCacheStream : Stream
    {
        private readonly IFunctionDataCache _functionDataCache;

        private bool _isDisposed;

        public FunctionDataCacheStream(FunctionDataCacheKey cacheKey, SharedMemoryMetadata sharedMemoryMeta, IFunctionDataCache functionDataCache)
        {
            CacheKey = cacheKey;
            SharedMemoryMetadata = sharedMemoryMeta;
            _functionDataCache = functionDataCache;
            _isDisposed = false;
        }

        public SharedMemoryMetadata SharedMemoryMetadata { get; private set; }

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