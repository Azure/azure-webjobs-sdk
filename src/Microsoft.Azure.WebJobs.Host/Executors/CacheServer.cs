// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Protocols;
using RangeTree;
using System;
using System.Collections.Concurrent;
using System.IO;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    // Note: The CacheServer relies on CacheObject being thread-safe
    public class CacheServer
    {
        public static readonly bool CacheEnabled = true; // Switch to enable/disable cache usage
        public static readonly bool CacheTriggersEnabled = CacheEnabled && true; // Switch to enable/disable cache trigger usage

        private readonly ConcurrentDictionary<CacheObjectMetadata, CacheObject> _inMemoryCache;
        public readonly ConcurrentQueue<CacheObjectMetadata> Triggers; // TODO call this something like processingtriggers
        public readonly ConcurrentBag<string> TriggersProcessedFromCache; // TODO call this something line processedtriggers and when to clean this periodically??

        // TODO make cacheclient register with this so that when a write happens to an object, invalidate all clients
        //      do that in a locked manner

        private CacheServer()
        {
            Triggers = new ConcurrentQueue<CacheObjectMetadata>();
            TriggersProcessedFromCache = new ConcurrentBag<string>();
            _inMemoryCache = new ConcurrentDictionary<CacheObjectMetadata, CacheObject>();
        }

        private static CacheServer instance = null;

        public static CacheServer Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new CacheServer();
                }
                return instance;
            }
        }

        public bool ContainsObject(CacheObjectMetadata cacheObjectMetadata)
        {
            return _inMemoryCache.ContainsKey(cacheObjectMetadata);
        }
        
        public bool ContainsObjectRange(CacheObjectMetadata cacheObjectMetadata, long start, long end)
        {
            if (!_inMemoryCache.TryGetValue(cacheObjectMetadata, out CacheObject cacheObject))
            {
                return false;
            }

            return ((CacheObjectStream)cacheObject).ContainsBytes(start, end);
        }
        
        public bool TryAddObjectStream(CacheObjectMetadata cacheObjectMetadata)
        {
            return _inMemoryCache.TryAdd(cacheObjectMetadata, new CacheObjectStream(cacheObjectMetadata));
        }
        
        public bool TryAddObjectStream(CacheObjectMetadata cacheObjectMetadata, MemoryStream memoryStream, bool commit)
        {
            if (_inMemoryCache.TryAdd(cacheObjectMetadata, new CacheObjectStream(cacheObjectMetadata, memoryStream)))
            {
                if (commit)
                {
                    this.CommitObjectToCache(cacheObjectMetadata);
                }

                return true;
            }
            else
            {
                return false;
            }
        }
        
        public void RemoveObject(CacheObjectMetadata cacheObjectMetadata)
        {
            _inMemoryCache.RemoveIfContainsKey(cacheObjectMetadata);
        }

        public bool TryGetObjectByteRangesAndStream(CacheObjectMetadata cacheObjectMetadata, out RangeTree<long, bool> byteRanges, out MemoryStream memoryStream)
        {
            byteRanges = null;
            memoryStream = null;
            
            if (!_inMemoryCache.TryGetValue(cacheObjectMetadata, out CacheObject cacheObject))
            {
                return false;
            }

            var data = ((CacheObjectStream)cacheObject).GetByteRangesAndBuffer().Result;
            byteRanges = data.Item1;
            // A read-only stream for the caller
            memoryStream = new MemoryStream(data.Item2, false);
            return true;
        }

        public bool TryGetObjectByteArray(CacheObjectMetadata cacheObjectMetadata, out byte[] buffer)
        {
            buffer = null;
            
            if (!_inMemoryCache.TryGetValue(cacheObjectMetadata, out CacheObject cacheObject))
            {
                return false;
            }

            var data = ((CacheObjectStream)cacheObject).GetByteRangesAndBuffer().Result;
            buffer = data.Item2;

            return true;
        }

        // TODO do we need to copy this buffer before returning? what if this buffer, which is owned by app, changes or something?
        public void WriteToCacheObject(CacheObjectMetadata cacheObjectMetadata, long startPosition, byte[] buffer, int offset, int count, int bytesToWrite)
        {
            if (_inMemoryCache.TryGetValue(cacheObjectMetadata, out CacheObject cacheObject))
            {
                ((CacheObjectStream)cacheObject).StartWriteTask(startPosition, buffer, offset, count, bytesToWrite);
            }
        }
        
        public void WriteToCacheObject(CacheObjectMetadata cacheObjectMetadata, long startPosition, byte value)
        {
            if (_inMemoryCache.TryGetValue(cacheObjectMetadata, out CacheObject cacheObject))
            {
                ((CacheObjectStream)cacheObject).StartWriteTask(startPosition, value);
            }
        }

        public void SetPosition(CacheObjectMetadata cacheObjectMetadata, long position)
        {
            if (_inMemoryCache.TryGetValue(cacheObjectMetadata, out CacheObject cacheObject))
            {
                ((CacheObjectStream)cacheObject).SetPosition(position);
            }
        }

        public bool Seek(CacheObjectMetadata cacheObjectMetadata, long offset, SeekOrigin origin, out long position)
        {
            position = -1;
            
            if (!_inMemoryCache.TryGetValue(cacheObjectMetadata, out CacheObject cacheObject))
            {
                return false;
            }

            position = ((CacheObjectStream)cacheObject).Seek(offset, origin);
            return true;
        }

        public void SetLength(CacheObjectMetadata cacheObjectMetadata, long length)
        {
            // TODO invalidate all clients since this is dirtying the cached object
            if (_inMemoryCache.TryGetValue(cacheObjectMetadata, out CacheObject cacheObject))
            {
                ((CacheObjectStream)cacheObject).SetLength(length);
            }
        }

        public bool GetPosition(CacheObjectMetadata cacheObjectMetadata, out long position)
        {
            position = -1;
            
            if (!_inMemoryCache.TryGetValue(cacheObjectMetadata, out CacheObject cacheObject))
            {
                return false;
            }

            position = ((CacheObjectStream)cacheObject).GetPosition();
            return true;
        }

        public void CommitObjectToCache(CacheObjectMetadata cacheObjectMetadata)
        {
            if (!_inMemoryCache.TryGetValue(cacheObjectMetadata, out CacheObject cacheObject))
            {
                return;
            }

            cacheObject.Commit();

            if (CacheTriggersEnabled)
            {
                Triggers.Enqueue(cacheObjectMetadata);
            }
        }
    }
}
