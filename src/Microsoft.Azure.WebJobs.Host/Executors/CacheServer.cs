// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Protocols;
using RangeTree;
using System;
using System.Collections.Concurrent;
using System.IO;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    public class CallbackConcurrentQueue<T>
    {
        public readonly ConcurrentQueue<T> Queue;
        public event EventHandler OnEnqueue;

        public CallbackConcurrentQueue()
        {
            Queue = new ConcurrentQueue<T>();
        }

        public void Enqueue(T value)
        {
            Queue.Enqueue(value);
            OnEnqueue(this, EventArgs.Empty);
        }

        public int Count
        {
            get
            {
                return Queue.Count;
            }
        }
        
        public bool TryDequeue(out T value)
        {
            return Queue.TryDequeue(out value);
        }
    }
    // Note: The CacheServer relies on CacheObject being thread-safe
    public class CacheServer
    {
        public static readonly bool CacheEnabled = true; // Switch to enable/disable cache usage
        public static readonly bool CacheTriggersEnabled = CacheEnabled && true; // Switch to enable/disable cache trigger usage

        private readonly ConcurrentDictionary<CacheObjectMetadata, CacheObject> _inMemoryCache;
        public readonly CallbackConcurrentQueue<CacheObjectMetadata> Triggers;
        public ConcurrentBag<string> TriggersProcessedFromCache; // TODO call this something line processedtriggers and when to clean this periodically??

        // TODO make cacheclient register with this so that when a write happens to an object, invalidate all clients
        //      do that in a locked manner

        private CacheServer()
        {
            TriggersProcessedFromCache = new ConcurrentBag<string>();
            Triggers = new CallbackConcurrentQueue<CacheObjectMetadata>();
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
        
        public bool TryAddObjectStream(CacheObjectMetadata cacheObjectMetadata, MemoryStream memoryStream, bool triggerCache)
        {
            // If we already have the object in the cache but are asked to trigger, then we trigger and return false as we did not add anything
            if (_inMemoryCache.ContainsKey(cacheObjectMetadata))
            {
                if (triggerCache)
                {
                    this.TriggerCache(cacheObjectMetadata);
                }

                return false;
            }

            if (_inMemoryCache.TryAdd(cacheObjectMetadata, new CacheObjectStream(cacheObjectMetadata, memoryStream)))
            {
                if (triggerCache)
                {
                    // If asked to trigger, and this was a new object we added, then commit (which will result in trigger too)
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
            TriggerCache(cacheObjectMetadata);
        }

        private void TriggerCache(CacheObjectMetadata cacheObjectMetadata)
        {
            if (CacheTriggersEnabled)
            {
                Triggers.Enqueue(cacheObjectMetadata);
            }
        }
    }
}
