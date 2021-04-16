// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// TODO
    /// </summary>
    public class CacheableObjectStream : Stream
    {
        private readonly Stream _inner;

        private readonly IFunctionDataCache _functionDataCache;

        private bool _isDisposed;

        public CacheableObjectStream(FunctionDataCacheKey cacheKey, Stream innerStream, IFunctionDataCache functionDataCache)
        {
            CacheKey = cacheKey;
            _inner = innerStream;
            _functionDataCache = functionDataCache;
            _isDisposed = false;
        }

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
