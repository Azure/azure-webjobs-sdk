// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Protocols;
using RangeTree;
using System;
using System.Collections.Concurrent;
using System.IO;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    public class CacheServer
    {
        private readonly ConcurrentDictionary<string, CacheObject> inMemoryCache = new ConcurrentDictionary<string, CacheObject>();

        // TODO make cacheclient register with this so that when a write happens to an object, invalidate all clients
        //      do that in a locked manner

        public bool ContainsObject(string uri)
        {
            return inMemoryCache.ContainsKey(uri);
        }
        
        public bool ContainsObjectRange(string uri, long start, long end)
        {
            if (!inMemoryCache.TryGetValue(uri, out CacheObject cacheObject))
            {
                return false;
            }

            // TODO Need locking here?
            return cacheObject.ContainsBytes(start, end);
        }
        
        public bool TryAddObject(string uri)
        {
            return inMemoryCache.TryAdd(uri, new CacheObject(uri));
        }

        // TODO maybe we don't need this, don't let clients handle CacheObjects
        // clients will just have memory stream
        public bool TryGetObject(string uri, out CacheObject cacheObject)
        {
            return inMemoryCache.TryGetValue(uri, out cacheObject);
        }

        public void RemoveObject(string uri)
        {
            inMemoryCache.RemoveIfContainsKey(uri);
        }

        public bool TryGetObjectByteRangesAndStream(string uri, out RangeTree<long, bool> byteRanges, out MemoryStream memoryStream)
        {
            byteRanges = null;
            memoryStream = null;

            if (!inMemoryCache.TryGetValue(uri, out CacheObject cacheObject))
            {
                return false;
            }

            var data = cacheObject.GetByteRangesAndBuffer().Result;
            byteRanges = data.Item1;
            // A read-only stream for the caller
            memoryStream = new MemoryStream(data.Item2, false);
            return true;
        }

        // TODO do we need to copy this buffer before returning? what if this buffer, which is owned by app, changes or something?
        public void WriteToCacheObject(string uri, long startPosition, byte[] buffer, int offset, int count)
        {
            if (inMemoryCache.TryGetValue(uri, out CacheObject cacheObject))
            {
                cacheObject.StartWriteTask(startPosition, buffer, offset, count);
            }
        }
        
        public void WriteToCacheObject(string uri, long startPosition, byte value)
        {
            if (inMemoryCache.TryGetValue(uri, out CacheObject cacheObject))
            {
                cacheObject.StartWriteTask(startPosition, value);
            }
        }

        public void SetPosition(string uri, long position)
        {
            if (inMemoryCache.TryGetValue(uri, out CacheObject cacheObject))
            {
                cacheObject.SetPosition(position);
            }
        }

        public bool Seek(string uri, long offset, SeekOrigin origin, out long position)
        {
            position = -1;

            if (!inMemoryCache.TryGetValue(uri, out CacheObject cacheObject))
            {
                return false;
            }

            position = cacheObject.Seek(offset, origin);
            return true;
        }

        public void SetLength(string uri, long length)
        {
            // TODO invalidate all clients since this is dirtying the cached operation
            if (inMemoryCache.TryGetValue(uri, out CacheObject cacheObject))
            {
                cacheObject.SetLength(length);
            }
        }

        public bool GetPosition(string uri, out long position)
        {
            position = -1;

            if (!inMemoryCache.TryGetValue(uri, out CacheObject cacheObject))
            {
                return false;
            }

            position = cacheObject.GetPosition();
            return true;
        }
    }
}
