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
        private static readonly HashSet<string> commitedStreams = new HashSet<string>();

        private readonly string _key;
        private readonly List<Task> _tasks;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public CacheClient(string key)
        {
            _key = key;
            _tasks = new List<Task>();

            if (this.ContainsKey())
            {
                if (inMemoryCache.TryGetValue(_key, out MemoryStream memoryStream))
                {
                    memoryStream.Seek(0, SeekOrigin.Begin);
                }
            }

            _cancellationTokenSource = new CancellationTokenSource();
        }

        ~CacheClient()
        {
            if (_tasks.TrueForAll(t => t.IsCompleted))
            {
                commitedStreams.Add(_key);
            }
            else
            {
                _cancellationTokenSource.Cancel();
                inMemoryCache.RemoveIfContainsKey(_key);
            }
        }

        public bool ContainsKey()
        {
            return commitedStreams.Contains(_key);
        }

        public Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            if (!this.ContainsKey())
            {
                inMemoryCache.TryAdd(_key, new MemoryStream());
            }

            if (inMemoryCache.TryGetValue(_key, out MemoryStream memoryStream))
            {
                Task baseTask = memoryStream.CopyToAsync(destination, bufferSize, cancellationToken);
                return baseTask;
            }
            else
            {
                // TODO fix me
                return null;
            }
        }
        
        public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!this.ContainsKey())
            {
                inMemoryCache.TryAdd(_key, new MemoryStream());
            }

            if (inMemoryCache.TryGetValue(_key, out MemoryStream memoryStream))
            {
                Task<int> baseTask = memoryStream.ReadAsync(buffer, offset, count, cancellationToken);
                return baseTask;
            }
            else
            {
                // TODO fix me
                return null;
            }
        }
        
        // TODO only difference is cancellation token
        public Task<int> ReadAsync(byte[] buffer, int offset, int count)
        {
            if (!this.ContainsKey())
            {
                // TODO error out
            }

            if (inMemoryCache.TryGetValue(_key, out MemoryStream memoryStream))
            {
                Task<int> baseTask = memoryStream.ReadAsync(buffer, offset, count);
                return baseTask;
            }
            else
            {
                // TODO fix me
                return null;
            }
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
        
        public void StartWriteTask(byte[] buffer, int offset, int count)
        {
            if (!inMemoryCache.ContainsKey(_key))
            {
                inMemoryCache.TryAdd(_key, new MemoryStream());
            }

            if (inMemoryCache.TryGetValue(_key, out MemoryStream memoryStream))
            {
                Task writeAsyncTask = memoryStream.WriteAsync(buffer, offset, count, _cancellationTokenSource.Token);
                _tasks.Add(writeAsyncTask);
            }
            else
            {
                // TODO problem
            }
        }

        // TODO wrap the checks and pass the core job as lambda so we can reuse the checks in above function too
        public void WriteByte(byte value)
        {
            if (!inMemoryCache.ContainsKey(_key))
            {
                inMemoryCache.TryAdd(_key, new MemoryStream());
            }

            if (inMemoryCache.TryGetValue(_key, out MemoryStream memoryStream))
            {
                memoryStream.WriteByte(value);
            }
            else
            {
                // TODO problem
            }
        }
    }
}
