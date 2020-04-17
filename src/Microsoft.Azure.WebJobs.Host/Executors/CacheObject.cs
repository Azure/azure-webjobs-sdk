// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Converters;
using RangeTree;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    [Serializable]
    public class CacheObject
    {
        private readonly CacheObjectMetadata _cacheObjectMetadata;
        private readonly RangeTree<long, bool> _byteRanges;
        private readonly MemoryStream _memoryStream;
        private readonly List<Task> _tasks;
        private bool _isCommitted;
        // TODO this cancellation source should be held by client, and when the client is dying, it cancels tasks
        // Then the cache object needs to invalidate itself and remove itself from cache
        // perhaps cancellation token should be with the cache server, and not cache client?
        // when cache client dies, it just infroms the cache server and dies - then asynchronously cache server 
        // will remove cache object after cancelling cache object's tasks
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly SemaphoreSlim _semaphore;
        
        public CacheObject(CacheObjectMetadata metadata)
        {
            _cacheObjectMetadata = metadata;
            _byteRanges = new RangeTree<long, bool>();
            _memoryStream = new MemoryStream();
            _tasks = new List<Task>();
            _cancellationTokenSource = new CancellationTokenSource();
            _semaphore = new SemaphoreSlim(1, 1); // Allow only one write at a time
            _isCommitted = false;
        }

        // TODO wrap all uses of the semaphore in this class inside try/finally
        public bool IsCommitted()
        {
            try
            {
                _semaphore.Wait();
                return _isCommitted;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        public void Commit()
        {
            try
            {
                _semaphore.Wait();
                _isCommitted = true;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public override int GetHashCode()
        {
            return _cacheObjectMetadata.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is CacheObject other))
            {
                return false;
            }

            return other._cacheObjectMetadata == this._cacheObjectMetadata;
        }

        public bool ContainsBytes(long start, long end)
        {
            //return _byteRanges.Query(start, end).Count() > 0;

            // REDUNDANTLY PUT IN CACHECLIENT.CS TOO SO CLEAN UP LATER
            // TODO this is really REALLY bad but keeping it here temporarily for correctness
            // using the _byteRanges.Query option has some issues:
            // e.g. if the range we have in the tree is 0-5, and we query for 0-10, it will come out to be true
            // That is because there is at least some overlap but we want all points in our range to have overlap
            // So might want to modify the rangetree data structure
            _semaphore.Wait();
            bool response = true;
            for (long i = start; i < end; i++)
            {
                if (_byteRanges.Query(i).Count() == 0)
                {
                    response = false;
                    break;
                }
            }
            _semaphore.Release();

            return response;
        }
        
        public bool ContainsByte(long key)
        {
            _semaphore.Wait();
            bool response = _byteRanges.Query(key).Count() > 0;
            _semaphore.Release();
            return response;
        }

        public async Task<Tuple<RangeTree<long, bool>, byte[]>> GetByteRangesAndBuffer()
        {
            await _semaphore.WaitAsync();
            try
            {
                // TODO check if this is a good option
                // MemoryStream uses a min buffer size of 256 most probably
                // Problem is, if we have fewer than 256 bytes, this will give us a bigger than the real buffer and the readers of this stream (when doing something like ReadToEndAsync etc) will cause issues as they read more than what the *actual* stream would have read
                if (_memoryStream.Length < 256)
                {
                    return Tuple.Create(_byteRanges, _memoryStream.ToArray());
                }
                else
                {
                    return Tuple.Create(_byteRanges, _memoryStream.GetBuffer());
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        // TODO technically, we should not need to take startPosition as input and should just write from current position
        // The reason we need to set the position explicitly is because for some weird reason, the position wobbles around 
        // For example when running the model.zip file
        public void StartWriteTask(long startPosition, byte[] buffer, int offset, int count, int bytesToWrite)
        {
            Task writeAsyncTask = StartWriteTaskCore(startPosition, buffer, offset, count, bytesToWrite);
            _tasks.Add(writeAsyncTask);
        }

        private async Task StartWriteTaskCore(long startPosition, byte[] buffer, int offset, int count, int bytesToWrite)
        {
            await _semaphore.WaitAsync();
            try
            {
                _memoryStream.Position = startPosition;
                await _memoryStream.WriteAsync(buffer, offset, bytesToWrite, _cancellationTokenSource.Token);
                //_byteRanges.Add(startPosition, _memoryStream.Position - 1, true);

                // TODO right now we store the range for which a read call was made and not the bytes that were actually read
                // e.g. when ReadToEnd is called, and let's say the stream is of size 10 bytes, the ReadToEnd will still call Read with count == 1024
                // The bytes read will be from 0 to 10 but the next time we do ReadToEnd again (on the cached stream) it will still call with count == 1024 and will get a cache miss because we just track 0 to 10 in the rangetree 
                // In fact, we should track the entire range for which the call was made regardless of how many bytes were read
                // This will ensure that the same call (or a call with overlapping region) will find some data in the cache
                // Approach B:
                // We can also do partial cache hit - if we ask for 0 to 1024 we can server 0 to 10 from cache and remaining from actual stream
                _byteRanges.Add(startPosition, startPosition + count - 1, true);
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
                _byteRanges.Add(startPosition, _memoryStream.Position - 1, true);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        public void SetPosition(long position)
        {
            _semaphore.Wait();
            _memoryStream.Position = position;
            _semaphore.Release();
        }

        public long Seek(long offset, SeekOrigin origin)
        {
            _semaphore.Wait();
            long response = _memoryStream.Seek(offset, origin);
            _semaphore.Release();
            return response;
        }

        public void SetLength(long length)
        {
            _semaphore.Wait();
            _memoryStream.SetLength(length);
            _semaphore.Release();
        }

        public long GetPosition()
        {
            _semaphore.Wait();
            long response = _memoryStream.Position;
            _semaphore.Release();
            return response;
        }
    }
}
