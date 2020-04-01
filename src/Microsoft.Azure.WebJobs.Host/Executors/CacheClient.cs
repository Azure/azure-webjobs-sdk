// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Protocols;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    // TODO mark the stream as "ready to read" after all write tasks for it are done
    // TODO make sure if someone is reading the same item from cache then write task blocks etc
    //      or put all the operations into a global client-wide lock
    // TODO check for all tasks in the _tasks list before destroying the client, should we wait? not a great idea 
    public class CacheClient
    {
        // TODO check what guarantees we have about multiple readers reading from same stream (may change position) - not cool
        private static readonly ConcurrentDictionary<string, MemoryStream> inMemoryCache = new ConcurrentDictionary<string, MemoryStream>();

        private readonly string _key;
        private readonly List<Task> _tasks;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly MemoryStream _memoryStream;
        private bool _isFlushedToCache;
        private bool _cacheHit;

        public bool CacheHit
        {
            get { return _cacheHit; }
            set { _cacheHit = value; }
        }

        public CacheClient(string key, long length = -1)
        {
            _key = key;
            _tasks = new List<Task>();
            _memoryStream = null;
            _isFlushedToCache = false;
            _cacheHit = false;

            if (this.ContainsKey())
            {
                if (inMemoryCache.TryGetValue(_key, out MemoryStream memoryStream))
                {
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    _memoryStream = memoryStream;
                }
            }
            else
            {
                if (_memoryStream == null)
                {
                    _memoryStream = new MemoryStream();
                    if (length != -1)
                    {
                        _memoryStream.SetLength(length);
                    }
                }
            }

            _cancellationTokenSource = new CancellationTokenSource();
        }

        public bool FlushToCache()
        {
            if (_tasks.TrueForAll(t => t.IsCompleted))
            {
                if (!inMemoryCache.TryAdd(_key, _memoryStream))
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

        ~CacheClient()
        {
            if (!_isFlushedToCache)
            {
                if (!FlushToCache())
                {
                    _cancellationTokenSource.Cancel();
                    inMemoryCache.RemoveIfContainsKey(_key);
                }
            }
        }

        public bool ContainsKey()
        {
            return inMemoryCache.ContainsKey(_key);
        }

        public Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            if (!this.ContainsKey())
            {
                // TODO error out
                return null;
            }

            Task baseTask = _memoryStream.CopyToAsync(destination, bufferSize, cancellationToken);
            return baseTask;
        }
        
        public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!this.ContainsKey())
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
            if (!this.ContainsKey())
            {
                // TODO error out
                return null;
            }

            Task<int> baseTask = _memoryStream.ReadAsync(buffer, offset, count);
            return baseTask;
        }
        
        // TODO TEMP FUNCTION
        public Task<int> ReadBackAsync(byte[] buffer, int offset, int count)
        {
            Task<int> baseTask = _memoryStream.ReadAsync(buffer, offset, count);
            return baseTask;
        }
        
        public int ReadByte()
        {
            if (!this.ContainsKey())
            {
                // TODO error out
            }

            if (inMemoryCache.TryGetValue(_key, out MemoryStream memoryStream))
            {
                return memoryStream.ReadByte();
            }
            else
            {
                // TODO fix me
                return -1;
            }
        }
        
        //public void StartWriteTask(byte[] buffer, int offset, int count)
        public Task StartWriteTask(byte[] buffer, int offset, int count)
        {
            if (this.ContainsKey())
            {
                // TODO invalidate the previous stream if we are attempting to write to it again
            }

            Task writeAsyncTask = _memoryStream.WriteAsync(buffer, offset, count, _cancellationTokenSource.Token);
            // TODO temp
            _tasks.Add(writeAsyncTask);
            return writeAsyncTask;
        }

        // TODO wrap the checks and pass the core job as lambda so we can reuse the checks in above function too
        public void WriteByte(byte value)
        {
            if (this.ContainsKey())
            {
                // TODO invalidate the previous stream if we are attempting to write to it again
            }

            _memoryStream.WriteByte(value);
        }

        public void SetPosition(long position)
        {
            _memoryStream.Position = position;
        }

        public long Seek(long offset, SeekOrigin origin)
        {
            return _memoryStream.Seek(offset, origin);
        }

        public void SetLength(long length)
        {
            _memoryStream.SetLength(length);
        }

        public long GetPosition()
        {
            return _memoryStream.Position;
        }
    }
}
