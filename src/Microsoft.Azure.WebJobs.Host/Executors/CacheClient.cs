// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Protocols;
using RangeTree;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    // TODO right now, this cache is focused on reading from blob store
    // When writing, easy case is, you write whatever app writes to your cached version
    // What happens when multiple entities all write to same object?
    // This includes SetLength operation too

    public class CacheClient
    {
        private static readonly CacheServer CacheServer = new CacheServer();

        private readonly string _key;
        private readonly RangeTree<long, bool> _byteRanges;
        private readonly MemoryStream _memoryStream;

        public bool CacheHit { get; set; }

        public bool ReadFromCache { get; private set; }

        // public CacheClient(string key, long length = -1)
        public CacheClient(string key)
        {
            _key = key;

            // TODO fix usage of this if needed
            CacheHit = true;

            if (CacheServer.TryGetObjectByteRangesAndStream(_key, out _byteRanges, out _memoryStream))
            {
                ReadFromCache = true;
            }
            else
            {
                ReadFromCache = false;
                _memoryStream = null;
                _byteRanges = null;

                // TODO length to cache object?
                // TODO check return value? and log?
                CacheServer.TryAddObject(_key);
            }
        }

        /*
        // TODO put this in the cache object?
        public bool FlushToCache()
        {
            if (_tasks.TrueForAll(t => t.IsCompleted))
            {
                if (!CacheServer.TryAdd(_key, _memoryStream))
                {
                    // TODO log error? (this can happen if key already there)
                }

                _isFlushedToCache = true;
                return true;
            }
            else
            {
                return false;
            }
        }
        */

        ~CacheClient()
        {
            /*
            if (!_isFlushedToCache)
            {
                if (!FlushToCache())
                {
                    _cancellationTokenSource.Cancel();
                    CacheServer.RemoveIfContainsKey(_key);
                }
            }
            */
        }

        public Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            if (!ReadFromCache)
            {
                // TODO error out
                return null;
            }

            Task baseTask = _memoryStream.CopyToAsync(destination, bufferSize, cancellationToken);
            return baseTask;
        }

        public bool ByteRangeInCache(long start, long end)
        {
            return _byteRanges.Query(start, end).Count() > 0;
        }
        
        public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!ReadFromCache)
            {
                // TODO error out
                return null;
            }

            Task<int> baseTask = _memoryStream.ReadAsync(buffer, offset, count, cancellationToken);
            return baseTask;
        }
        
        // TODO only difference is cancellation token
        public Task<int> ReadAsync(byte[] buffer, int offset, int count)
        {
            if (!ReadFromCache)
            {
                // TODO error out
                return null;
            }

            Task<int> baseTask = _memoryStream.ReadAsync(buffer, offset, count);
            return baseTask;
        }
        
        public int ReadByte()
        {
            if (!ReadFromCache)
            {
                // TODO error out
                return -1;
            }

            return _memoryStream.ReadByte();
        }

        public void StartWriteTask(long startPosition, byte[] buffer, int offset, int count)
        {
            ReadFromCache = false;
            CacheServer.WriteToCacheObject(_key, startPosition, buffer, offset, count);
        }

        // TODO wrap the checks and pass the core job as lambda so we can reuse the checks in above function too
        public void WriteByte(long startPosition, byte value)
        {
            ReadFromCache = false;
            CacheServer.WriteToCacheObject(_key, startPosition, value);
        }

        public void SetPosition(long position)
        {
            if (!ReadFromCache)
            {
                CacheServer.SetPosition(_key, position);
            }
            else
            {
                _memoryStream.Position = position;
            }
        }

        public long Seek(long offset, SeekOrigin origin)
        {
            if (!ReadFromCache)
            {
                if (CacheServer.Seek(_key, offset, origin, out long position))
                {
                    return position;
                }
                else
                {
                    // TODO log failure
                    return -1;
                }
            }
            else
            {
                return _memoryStream.Seek(offset, origin);
            }
        }

        public void SetLength(long length)
        {
            // If length is being set, then this operation will change the cached entity
            // So no operation can be performed on the cached stream
            // Server will invalidate all clients here
            CacheServer.SetLength(_key, length);
        }

        public long GetPosition()
        {
            if (!ReadFromCache)
            {
                if (CacheServer.GetPosition(_key, out long position))
                {
                    return position;
                }
                else
                {
                    // TODO log failure
                    return -1;
                }
            }
            else
            {
                return _memoryStream.Position;
            }
        }

        // TODO as soon as we invalidate the cache stream from here, we need to make sure the actual stream's position (azure) is same as the cached one until now
    }
}
