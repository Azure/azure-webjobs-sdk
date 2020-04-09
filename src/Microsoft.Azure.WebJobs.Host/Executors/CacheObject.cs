// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RangeTree;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    public class CacheObject
    {
        private readonly string _uri;
        private readonly RangeTree<long, bool> _byteRanges;
        // TODO may be worthwhile to start using BufferedStream in the future; faster?
        private readonly MemoryStream _memoryStream;
        private readonly List<Task> _tasks;
        // TODO this cancellation source should be held by client, and when the client is dying, it cancels tasks
        // Then the cache object needs to invalidate itself and remove itself from cache
        // perhaps cancellation token should be with the cache server, and not cache client?
        // when cache client dies, it just infroms the cache server and dies - then asynchronously cache server 
        // will remove cache object after cancelling cache object's tasks
        private readonly CancellationTokenSource _cancellationTokenSource;
        private SemaphoreSlim _semaphore;

        public CacheObject(string uri)
        {
            _uri = uri;
            _byteRanges = new RangeTree<long, bool>();
            _memoryStream = new MemoryStream();
            _tasks = new List<Task>();
            _cancellationTokenSource = new CancellationTokenSource();
            _semaphore = new SemaphoreSlim(1, 1); // Allow only one write at a time
        }

        public override int GetHashCode()
        {
            return _uri.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is CacheObject other))
            {
                return false;
            }

            return other._uri == this._uri;
        }

        public bool ContainsBytes(long start, long end)
        {
            return _byteRanges.Query(start, end).Count() > 0;
        }
        
        public bool ContainsByte(long key)
        {
            return _byteRanges.Query(key).Count() > 0;
        }

        public async Task<Tuple<RangeTree<long, bool>, byte[]>> GetByteRangesAndBuffer()
        {
            await _semaphore.WaitAsync();
            try
            {
                return Tuple.Create(_byteRanges, _memoryStream.GetBuffer());
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        // TODO technically, we should not need to take startPosition as input and should just write from current position
        // The reason we need to set the position explicitly is because for some weird reason, the position wobbles around 
        // For example when running the model.zip file
        public void StartWriteTask(long startPosition, byte[] buffer, int offset, int count)
        {
            Task writeAsyncTask = StartWriteTaskCore(startPosition, buffer, offset, count);
            _tasks.Add(writeAsyncTask);
        }

        private async Task StartWriteTaskCore(long startPosition, byte[] buffer, int offset, int count)
        {
            await _semaphore.WaitAsync();
            try
            {
                _memoryStream.Position = startPosition;
                await _memoryStream.WriteAsync(buffer, offset, count, _cancellationTokenSource.Token);
                _byteRanges.Add(startPosition, _memoryStream.Position, true);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        public void StartWriteTask(long startPosition, byte value)
        {
            Task writeAsyncTask = StartWriteTaskCore(startPosition, value);
            _tasks.Add(writeAsyncTask);
        }

        private async Task StartWriteTaskCore(long startPosition, byte value)
        {
            await _semaphore.WaitAsync();
            try
            {
                _memoryStream.Position = startPosition;
                _memoryStream.WriteByte(value);
                _byteRanges.Add(startPosition, _memoryStream.Position, true);
            }
            finally
            {
                _semaphore.Release();
            }
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
