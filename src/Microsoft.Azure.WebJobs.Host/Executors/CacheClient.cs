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
    // TODO make the CacheServer invalidate all clients when a write is made (use callbacks)
    public class CacheClient
    {
        private readonly CacheObjectMetadata _cacheObjectMetadata;
        private readonly RangeTree<long, bool> _byteRanges;
        private readonly MemoryStream _memoryStream;
        private readonly CacheServer _cacheServer = CacheServer.Instance;
        private readonly bool _toCommit; // TODO pass in more metadata and commit only for output bindings (i.e. do this more cleanly)

        public CacheClient(CacheObjectMetadata cacheObjectMetadata, bool isWriteObject)
        {
            _cacheObjectMetadata = cacheObjectMetadata;

            if (!isWriteObject && _cacheServer.TryGetObjectByteRangesAndStream(_cacheObjectMetadata, out _byteRanges, out _memoryStream))
            {
                ReadFromCache = true;
                _toCommit = false;
            }
            else
            {
                ReadFromCache = false;
                _memoryStream = null;
                _byteRanges = null;
                _toCommit = isWriteObject;

                // TODO length to cache object?
                // TODO check return value? and log?
                _cacheServer.TryAddObjectStream(_cacheObjectMetadata);
            }
        }
        
        public bool ReadFromCache { get; private set; }

        public bool CanRead()
        {
            return _memoryStream.CanRead;
        }
        
        public bool CanSeek()
        {
            return _memoryStream.CanSeek;
        }
        
        public bool CanTimeout()
        {
            return _memoryStream.CanTimeout;
        }
        
        public bool CanWrite()
        {
            return _memoryStream.CanWrite;
        }
        
        public long GetLength()
        {
            return _memoryStream.Length;
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
            //for (long i = start; i < end; i++)
            //{
            //    if (_byteRanges.Query(i).Count() == 0)
            //    {
            //        return false;
            //    }
            //}

            return true; // TODO HACKKKK
        }
        
        public bool ContainsByte(long key)
        {
            //return _byteRanges.Query(key).Count() > 0;
            return true; // TODO HACKKKK
        }

        public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // Caller's responsibility to check if byte range is in cache
            if (!ReadFromCache)
            {
                // TODO error out
                return null;
            }

            // Check if the byte range needed to be read is present in cache 
            Task<int> baseTask = _memoryStream.ReadAsync(buffer, offset, count, cancellationToken);
            return baseTask;
        }
        
        public Task<int> ReadAsync(byte[] buffer, int offset, int count)
        {
            // Caller's responsibility to check if byte range is in cache
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
            // Caller's responsibility to check if byte range is in cache
            if (!ReadFromCache)
            {
                // TODO error out
                return -1;
            }

            return _memoryStream.ReadByte();
        }

        public void StartWriteTask(long startPosition, byte[] buffer, int offset, int count, int bytesToWrite)
        {
            // As soon as the cached object is written to, it will be invalidated and all subsequent reads will not be made from this cached object
            // TODO think more about this policy
            ReadFromCache = false;

            // Sometimes the bytes written by the actual stream are not known so we attempt to write count number of bytes
            if (bytesToWrite == -1)
            {
                bytesToWrite = count;
            }

            // Deep copy the buffer before the caller can modify it
            byte[] bufferToWrite = buffer.ToArray();

            _cacheServer.WriteToCacheObject(_cacheObjectMetadata, startPosition, bufferToWrite, offset, count, bytesToWrite);
        }

        public void WriteByte(long startPosition, byte value)
        {
            ReadFromCache = false;

            _cacheServer.WriteToCacheObject(_cacheObjectMetadata, startPosition, value);
        }

        public void SetPosition(long position)
        {
            if (!ReadFromCache)
            {
                _cacheServer.SetPosition(_cacheObjectMetadata, position);
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
                if (_cacheServer.Seek(_cacheObjectMetadata, offset, origin, out long position))
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
            _cacheServer.SetLength(_cacheObjectMetadata, length);
        }

        public long GetPosition()
        {
            if (!ReadFromCache)
            {
                if (_cacheServer.GetPosition(_cacheObjectMetadata, out long position))
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

        public void Close()
        {
            _memoryStream?.Close();
            if (_toCommit)
            {
                _cacheServer.CommitObjectToCache(_cacheObjectMetadata);
            }
        }
    }
}
