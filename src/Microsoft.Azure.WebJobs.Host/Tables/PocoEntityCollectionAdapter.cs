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
    internal class PocoEntityCollectionAdapter<T> : ICollector<T>, IAsyncCollector<T>
    {
        private TableEntityCollectionAdapter<DynamicTableEntity> _tableEntityCollectionAdapter;

        /// <summary>
        /// The table collection adapter.
        /// </summary>
        /// <param name="table"></param>
        /// <param name="partitionKey"></param>
        /// <param name="access"></param>
        public PocoEntityCollectionAdapter(CloudTable table, string partitionKey = null, FileAccess access = FileAccess.ReadWrite)
        {
            _tableEntityCollectionAdapter = new TableEntityCollectionAdapter<DynamicTableEntity>(table, partitionKey, access);
        }

        /// <summary>
        /// Adds an item asynchronously.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public async Task AddAsync(T item)
        {
            DynamicTableEntity tableEntity = PocoTableEntity.ToTableEntity(item) as DynamicTableEntity;
            await _tableEntityCollectionAdapter.AddAsync(tableEntity);
        }

        /// <summary>
        /// Adds an item.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public void Add(T item)
        {
            AddAsync(item).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Flush any queued up operations.
        /// </summary>
        /// <returns></returns>
        internal async Task FlushAllAsync()
        {
            await _tableEntityCollectionAdapter.FlushAllAsync();
        }
    }
}