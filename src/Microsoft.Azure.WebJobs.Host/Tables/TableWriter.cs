// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Tables
{
    /// <summary>
    /// The table writer.
    /// </summary>
    internal class TableWriter
    {
        private readonly CloudTable _table;

        /// <summary>
        /// Max batch size is an azure limitation on how many entries can be in each batch.
        /// </summary>
        public const int MaxBatchSize = 100;

        /// <summary>
        /// how many different partition keys do we cache offline before flushing?
        /// This means the max offline cache size is (MaxPartitionWidth * (MaxBatchSize-1)) entries.
        /// </summary>
        public const int MaxPartitionWidth = 1000;

        /// <summary>
        /// Map PartitionKey --> RowKey --> ITableEntity
        /// </summary>
        private readonly Dictionary<string, Dictionary<string, ITableEntity>> _map = new Dictionary<string, Dictionary<string, ITableEntity>>();

        private readonly TableStatistics _stats;

        /// <summary>
        /// The table writer.
        /// </summary>
        /// <param name="table"></param>
        /// <param name="stats"></param>
        public TableWriter(CloudTable table, TableStatistics stats)
        {
            _table = table;
            _stats = stats;
        }

        /// <summary>
        /// Adds an element asynchronously
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public async Task AddAsync(ITableEntity item)
        {
            _stats.WriteCount++;

            // Careful: 
            // 1. even with upsert, all rowkeys within a batch must be unique. Take latest. 
            // 2. Capture at time of Add, in case item is mutated after add. 
            // 3. Validate rowkey on the client so we get a nice error instead of the cryptic 400 from auzre.

            string partKey = item.PartitionKey;
            string rowKey = item.RowKey;

            TableClient.ValidateAzureTableKeyValue(item.RowKey);

            Dictionary<string, ITableEntity> partition;
            if (!_map.TryGetValue(partKey, out partition))
            {
                if (_map.Count >= MaxPartitionWidth)
                {
                    // Offline cache is too large. Clear some room
                    await FlushAllAsync();
                }

                partition = new Dictionary<string, ITableEntity>();
                _map[partKey] = partition;
            }

            var itemCopy = Copy(item);
            partition[rowKey] = itemCopy;

            if (partition.Count >= MaxBatchSize)
            {
                await FlushPartitionAsync(partition);
                _map.Remove(partKey);
            }
        }

        private static ITableEntity Copy(ITableEntity item)
        {
            var ctx = new OperationContext();
            var props = item.WriteEntity(ctx);
            DynamicTableEntity copy = new DynamicTableEntity(item.PartitionKey, item.RowKey, item.ETag, props);
            return copy;
        }

        /// <summary>
        /// Offline cache is too large. Flush part of it. 
        /// </summary>
        /// <returns></returns>
        public Task FlushPartialAsync()
        {
            // For simplification, we'll just flush the whole cache. 
            // This can be a perf hit since it flushes low volume-partitions.
            // But we could be clever and only flush a few partitions. 
            return FlushAllAsync();
        }

        /// <summary>
        /// Flushes everything.
        /// </summary>
        /// <returns></returns>
        public async Task FlushAllAsync()
        {
            foreach (var kv in _map)
            {
                await FlushPartitionAsync(kv.Value);
            }
            _map.Clear();
        }

        /// <summary>
        /// Flushes the partition key
        /// </summary>
        /// <param name="partition"></param>
        /// <returns></returns>
        public async Task FlushPartitionAsync(Dictionary<string, ITableEntity> partition)
        {
            TableBatchOperation batch = new TableBatchOperation();

            foreach (var entity in partition.Values)
            {
                batch.Add(TableOperation.InsertOrReplace(entity));
            }
            if (batch.Count > 0)
            {
                try
                {
                    _stats.WriteIOTime.Start();
                    await _table.ExecuteBatchAsync(batch);
                }
                finally
                {
                    _stats.WriteIOTime.Stop();
                }
            }
        }
    }
}
