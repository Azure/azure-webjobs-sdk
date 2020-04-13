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

        private readonly CacheObjectMetadata _cacheObjectMetadata;
        private readonly RangeTree<long, bool> _byteRanges;
        private readonly MemoryStream _memoryStream;

        public bool CacheHit { get; set; }

        public bool ReadFromCache { get; private set; }

        // public CacheClient(string key, long length = -1)
        public CacheClient(string uri, string etag)
        {
            _cacheObjectMetadata = new CacheObjectMetadata(uri, etag);

            // TODO fix usage of this if needed
            CacheHit = true;

            if (CacheServer.TryGetObjectByteRangesAndStream(_cacheObjectMetadata, out _byteRanges, out _memoryStream))
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
                CacheServer.TryAddObject(_cacheObjectMetadata);
            }
        }

        /*
        // TODO put this in the cache object?
        public bool FlushToCache()
        {
            if (_tasks.TrueForAll(t => t.IsCompleted))
            {
                if (!CacheServer.TryAdd(_cacheObjectMetadata, _memoryStream))
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
                    CacheServer.RemoveIfContainsKey(_cacheObjectMetadata);
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

        // TODO Wrap the RangeTree in custom class and put these methods there
        // Reuse in CacheObject too
        // TODO maybe change this method and below into one - ContainsNextNBytes from current position 
        public bool ContainsBytes(long start, long end)
        {
            //return _byteRanges.Query(start, end).Count() > 0;

            // REDUNDANTLY PUT IN CACHECLIENT.CS TOO SO CLEAN UP LATER
            // TODO this is really REALLY bad but keeping it here temporarily for correctness
            // using the _byteRanges.Query option has some issues:
            // e.g. if the range we have in the tree is 0-5, and we query for 0-10, it will come out to be true
            // That is because there is at least some overlap but we want all points in our range to have overlap
            // So might want to modify the rangetree data structure
            for (long i = start; i < end; i++)
            {
                if (_byteRanges.Query(i).Count() == 0)
                {
                    return false;
                }
            }
            return true;
        }
        
        public bool ContainsByte(long key)
        {
            return _byteRanges.Query(key).Count() > 0;
        }

        // TODO check for count > 0? or is that a perf hit?
        public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // TODO if bytes are not present, do we invalidate the cached object or what?
            //if (!ReadFromCache || !ContainsBytes(_memoryStream.Position, _memoryStream.Position + count))
            // TODO we don't need to check the bytes again - caller's responsibility to check if byte range is in cache
            if (!ReadFromCache)
            {
                // TODO error out
                return null;
            }

            // Check if the byte range needed to be read is present in cache 
            Task<int> baseTask = _memoryStream.ReadAsync(buffer, offset, count, cancellationToken);
            return baseTask;
        }
        
        // TODO only difference is cancellation token
        public Task<int> ReadAsync(byte[] buffer, int offset, int count)
        {
            // TODO we don't need to check the bytes again - caller's responsibility to check if byte range is in cache
            //if (!ReadFromCache || !ContainsBytes(_memoryStream.Position, _memoryStream.Position + count - 1))
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
            if (!ReadFromCache || !ContainsByte(_memoryStream.Position))
            {
                // TODO error out
                return -1;
            }

            return _memoryStream.ReadByte();
        }

        public void StartWriteTask(long startPosition, byte[] buffer, int offset, int count, int bytesToWrite)
        {
            ReadFromCache = false;
            if (bytesToWrite == -1)
            {
                bytesToWrite = count;
            }

            // Deep copy the buffer before the caller can modify it
            byte[] bufferToWrite = buffer.ToArray();

            CacheServer.WriteToCacheObject(_cacheObjectMetadata, startPosition, bufferToWrite, offset, count, bytesToWrite);
        }

        // TODO wrap the checks and pass the core job as lambda so we can reuse the checks in above function too
        public void WriteByte(long startPosition, byte value)
        {
            ReadFromCache = false;
            CacheServer.WriteToCacheObject(_cacheObjectMetadata, startPosition, value);
        }

        public void SetPosition(long position)
        {
            if (!ReadFromCache)
            {
                CacheServer.SetPosition(_cacheObjectMetadata, position);
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
                if (CacheServer.Seek(_cacheObjectMetadata, offset, origin, out long position))
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

        // TODO Make all such operations async - from the server also
        public void SetLength(long length)
        {
            // If length is being set, then this operation will change the cached entity
            // So no operation can be performed on the cached stream
            // Server will invalidate all clients here
            CacheServer.SetLength(_cacheObjectMetadata, length);
        }

        public long GetPosition()
        {
            if (!ReadFromCache)
            {
                if (CacheServer.GetPosition(_cacheObjectMetadata, out long position))
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
