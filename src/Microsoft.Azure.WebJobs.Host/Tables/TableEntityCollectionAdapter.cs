// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Tables
{
    /// <summary>
    /// See http://azure.microsoft.com/en-us/documentation/articles/storage-dotnet-how-to-use-tables/ 
    ///  for more details on using azure storage. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class TableEntityCollectionAdapter<T> : ICollector<T>, IAsyncCollector<T> where T : ITableEntity, new()
    {
        /// <summary>
        /// The table that we operate on.
        /// </summary>
        private readonly CloudTable _table;

        private readonly FileAccess _access;

        /// <summary>
        /// If set, all operations are scoped to this partition.
        /// </summary>
        private readonly string _partitionKey;

        private readonly TableWriter _writer;

        /// <summary>
        /// Do we have buffered-writes that need to be flushed.
        /// </summary>
        private bool _dirty;

        /// <summary>
        /// The table statistics.
        /// </summary>
        public TableStatistics Stats { get; private set; }

        /// <summary>
        /// The table collection adapter.
        /// </summary>
        /// <param name="table"></param>
        /// <param name="partitionKey"></param>
        /// <param name="access"></param>
        public TableEntityCollectionAdapter(CloudTable table, string partitionKey = null, FileAccess access = FileAccess.ReadWrite)
        {
            _table = table;
            _access = access;
            _partitionKey = partitionKey;

            Stats = new TableStatistics();
            _writer = new TableWriter(_table, this.Stats);
        }

        /// <summary>
        /// To string.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Stats.GetStatus();
        }

        /// <summary>
        /// Flush any queued up operations.
        /// </summary>
        /// <returns></returns>
        internal async Task FlushAllAsync()
        {
            _dirty = false;
            await _writer.FlushAllAsync();
        }

        private void FlushIfNeeded()
        {
            if (_dirty)
            {
                FlushAllAsync().Wait();
            }
        }

        /// <summary>
        /// Expose so ICollector can operate async.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public virtual async Task AddAsync(T item)
        {
            _dirty = true;
            VerifyCanWrite();
            await _writer.AddAsync(item);
        }

        /// <summary>
        /// Adds an item.
        /// </summary>
        /// <param name="item"></param>
        public virtual void Add(T item)
        {
            AddAsync(item).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Delete a single entity
        /// 
        /// Return true if removed. False if item is not in the collection.
        /// The return value means the delete must occur immediately and we can't batch it. 
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Remove(T item)
        {
            bool removed = RemoveAsync(item).Result;
            return removed;
        }

        /// <summary>
        /// Removes an item asynchronously
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public async Task<bool> RemoveAsync(T item)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            VerifyCanWrite();
            FlushIfNeeded();
            Stats.DeleteCount++;

            // Etag must be set. Use "*" to skip the etag match and do a force delete. 
            // http://blog.smarx.com/posts/deleting-entities-from-windows-azure-without-querying-first
            item.ETag = "*";

            var op = TableOperation.Delete(item);

            try
            {
                try
                {
                    Stats.DeleteIOTime.Start();
                    TableResult result = await _table.ExecuteAsync(op);
                }
                finally
                {
                    Stats.DeleteIOTime.Stop();
                }
            }
            catch (StorageException e)
            {
                var code = e.RequestInformation.HttpStatusCode;
                if (code == 404)
                {
                    // Item not in the table.
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Delete the entire section
        /// </summary>
        public void Clear()
        {
            if (_partitionKey == null)
            {
                ClearTableAsync().Wait();
            }
            else
            {
                DeleteTablePartitionAsync(_partitionKey).Wait();
            }
        }

        /// <summary>
        /// Delete the partition
        /// </summary>
        /// <param name="partitionKey"></param>
        /// <returns></returns>
        public async Task DeleteTablePartitionAsync(string partitionKey)
        {
            if (partitionKey == null)
            {
                throw new ArgumentNullException("partitionKey");
            }

            VerifyCanWrite();
            FlushIfNeeded();

            // http://stackoverflow.com/questions/7393651/can-i-delete-an-entire-partition-in-windows-azure-table-storage
            // No Azure API to delete an entire partition. 
            // We have to enumerate the rows and delete in batches at a time.
            // At least we can batch up the Delete calls.

            int BatchSize = TableWriter.MaxBatchSize;

            TableQuery query = new TableQuery();
            var filter = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey);
            query = query.Where(filter);

            IEnumerable<DynamicTableEntity> results = _table.ExecuteQuery(query);

            // Delete them all 
            TableBatchOperation batch = new TableBatchOperation();
            foreach (var entity in results)
            {
                // Batches must be the same partition, but that's a given since we're enumerating a single partition
                batch.Add(TableOperation.Delete(entity));

                if (batch.Count >= BatchSize)
                {
                    await ExecuteDeleteBatchAsync(batch);
                }
            }

            // Flush remainder
            await ExecuteDeleteBatchAsync(batch);
        }

        private async Task ExecuteDeleteBatchAsync(TableBatchOperation batch)
        {
            if (batch.Count == 0)
            {
                return;
            }
            Stats.DeleteCount += batch.Count;

            try
            {
                Stats.DeleteIOTime.Start();
                await _table.ExecuteBatchAsync(batch);
            }
            finally
            {
                Stats.DeleteIOTime.Stop();
            }
            batch.Clear();
        }

        /// <summary>
        /// Deleting just means it's marked for delete and the Azure GC will take care of it.
        /// Can take over a minute for the GC to actually reclaim it.
        /// </summary>
        /// <returns></returns>
        public async Task ClearTableAsync()
        {
            VerifyCanWrite();
            FlushIfNeeded();

            try
            {
                Stats.DeleteIOTime.Start();

                // Clear all contents by deleting the table and recreating it. 
                // Alternatively, we could enumerate the table and delete each row. That would be faster
                // for small tables, but much slower for larger tables. Unfortunately, we don't know a table's size. 
                await _table.DeleteAsync();

                // Wait for it to recreate. This verifies we're back in a known state. 
                // Alternatively, We return optimistically even though we're in the 'deleting' state.                     
                await WaitForTableReadyAsync();
            }
            finally
            {
                Stats.DeleteIOTime.Stop();
            }
        }

        /// <summary>
        /// Wait for table ready asynchronously.
        /// </summary>
        /// <returns></returns>
        public async Task WaitForTableReadyAsync()
        {
            // Timeout is maximum time we wait for Azure's GC to reclaim a deleted table and make it available. 
            TimeSpan timeout = TimeSpan.FromMinutes(2);
            TimeSpan interval = TimeSpan.FromSeconds(3);
            while (true)
            {
                timeout -= interval;

                // Recreate it 
                try
                {
                    await _table.CreateIfNotExistsAsync();

                    return;
                }
                catch (StorageException e)
                {
                    // Timeout waiting for Table to become ready again. 
                    if (timeout < TimeSpan.Zero)
                    {
                        throw e;
                    }
                }

                await Task.Delay(interval);
            }
        }

        /// <summary>
        /// Return true if item exists, and writes it properties.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Contains(T item)
        {
            VerifyCanRead();
            FlushIfNeeded();
            var op = TableOperation.Retrieve<T>(item.PartitionKey, item.RowKey);

            TableResult retrievedResult;
            try
            {
                Stats.ReadCount++;
                Stats.ReadIOTime.Start();
                retrievedResult = _table.Execute(op);
            }
            finally
            {
                Stats.ReadIOTime.Stop();
            }

            var result = retrievedResult.Result;
            if (result == null)
            {
                return false;
            }

            var ite = (T)result;
            var ctx = new OperationContext();
            var props = ite.WriteEntity(ctx);
            item.ReadEntity(props, ctx);

            return true;
        }

        /// <summary>
        /// Copies to.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="arrayIndex"></param>
        public void CopyTo(T[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Count would require iterate the entire table.
        /// This is both slow and could also involve a lot of transactions (which are a billable event).
        /// Avoid implementing it to protect users from accidentally calling it.
        /// </summary>
        public int Count
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Is read only.
        /// </summary>
        public bool IsReadOnly
        {
            get
            {
                return _access == FileAccess.Read;
            }
        }

        private void VerifyCanRead()
        {
            if (_access == FileAccess.Write)
            {
                throw new InvalidOperationException("Table can not be read.");
            }
        }

        private void VerifyCanWrite()
        {
            if (_access == FileAccess.Read)
            {
                throw new InvalidOperationException("Table can not be written to.");
            }
        }

        /// <summary>
        /// Gets the enumerator.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<T> GetEnumerator()
        {
            return GetEnumeratorInternal();
        }

        private IEnumerator<T> GetEnumeratorInternal()
        {
            VerifyCanRead();
            FlushIfNeeded();

            TableQuery<T> query = new TableQuery<T>();

            if (_partitionKey == null)
            {
                // Enumerate all 
            }
            else
            {
                // Enumerate just this partition
                var filter = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, _partitionKey);
                query = query.Where(filter);
            }

            // Capture time in IO. Beware that query may implement paging and do IO in the middle
            Stats.ReadIOTime.Start();
            IEnumerable<T> results = _table.ExecuteQuery<T>(query);

            try
            {
                foreach (var result in results)
                {
                    Stats.ReadIOTime.Stop();
                    Stats.ReadCount++;
                    yield return result;
                    Stats.ReadIOTime.Start();
                }
            }
            finally
            {
                Stats.ReadIOTime.Stop();
            }
        }
    }
}