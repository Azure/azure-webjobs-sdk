// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.Azure.WebJobs.Logging
{
    // Default table provider for logging 
    internal class EpochTableProvider : IEpochTableProvider
    {
        private readonly CloudTableClient _tableClient;
        private readonly string _tableNamePrefix;

        public EpochTableProvider(CloudTableClient tableClient, string tableNamePrefix = LogFactory.DefaultLogTableName)
        {
            if (tableClient == null)
            {
                throw new ArgumentNullException("tableClient");
            }
            if (string.IsNullOrWhiteSpace(tableNamePrefix))
            {
                throw new ArgumentNullException("tableNamePrefix");
            }

            _tableNamePrefix = tableNamePrefix;            
            _tableClient = tableClient;
        }

        public CloudTable NewTable(string suffix)
        {
            var tableName = _tableNamePrefix + suffix;
            var table = _tableClient.GetTableReference(tableName);
            return table;
        }

        // List all tables that we may have handed out. 
        public async Task<CloudTable[]> ListTablesAsync()
        {
            List<CloudTable> list = new List<CloudTable>();
            TableContinuationToken continuationToken = null;
            while (true)
            {
                var segment = await _tableClient.ListTablesSegmentedAsync(_tableNamePrefix, continuationToken, CancellationToken.None);
                list.AddRange(segment.Results);

                if (segment.ContinuationToken == null)
                {
                    break;
                }
                continuationToken = segment.ContinuationToken;
            }

            return list.ToArray();
        }
    }
}