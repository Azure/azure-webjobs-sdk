// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Threading;

namespace Microsoft.Azure.WebJobs.Logging
{
    // Index that provides list of recention invocations per function type.
    // 1 entity per Intance of a function that's executed. 
    public class RecentPerFuncEntity : TableEntity
    {
        const string PartitionKeyFormat = TableScheme.RecentFuncIndexPK;
        const string RowKeyPrefix = "{0}-{1:D20}-";
        const string RowKeyFormat = "{0}-{1:D20}-{2}"; // functionName, timeBucket(descending), salt

        // Have a salt value for writing to avoid collisions since timeBucket is not gauranteed to be unique
        // when many functions are quickly run within a single time tick. 
        static int _salt;

        public static RecentPerFuncEntity New(string containerName, FunctionLogItem item)
        {
            return new RecentPerFuncEntity
            {
                PartitionKey = PartitionKeyFormat,
                RowKey = RowKeyTimeStampDescending(item.FunctionName, item.StartTime),

                FunctionName = item.FunctionName,
                DisplayName = item.GetDisplayTitle(),
                FunctionInstanceId = item.FunctionInstanceId.ToString(),
                StartTime = item.StartTime,
                EndTime = item.EndTime,
                ContainerName = containerName
            };
        }

        public static TableQuery<RecentPerFuncEntity> GetRecentFunctionsQuery(
            string functionName,
            DateTime start,
            DateTime end)
        {
            string rowKeyStart = RowKeyTimeStampDescendingPrefix(functionName, end);

            // add a tick to create a greater row key so that we lexically compare
            var start2 = (start == DateTime.MinValue) ? start : start.AddTicks(-1);
            string rowKeyEnd = RowKeyTimeStampDescendingPrefix(functionName, start2);

            string partKey = PartitionKeyFormat;

            var rangeQuery = TableScheme.GetRowsInRange<RecentPerFuncEntity>(
                partKey, rowKeyStart, rowKeyEnd);
            return rangeQuery;
        }


        // No salt. This is a prefix, so we'll pick up all ranges.
        private static string RowKeyTimeStampDescendingPrefix(string functionName, DateTime startTime)
        {
            var x = (DateTime.MaxValue.Ticks - startTime.Ticks);

            string rowKey = string.Format(RowKeyPrefix, TableScheme.NormalizeFunctionName(functionName), x);
            return rowKey;
        }

        internal static string RowKeyTimeStampDescending(string functionName, DateTime startTime)
        {
            var x = (DateTime.MaxValue.Ticks - startTime.Ticks);

            // Need Salt since timestamp may not be unique
            int salt = Interlocked.Increment(ref _salt);
            string rowKey = string.Format(RowKeyFormat, TableScheme.NormalizeFunctionName(functionName), x, salt);
            return rowKey;
        }

        public string ContainerName { get; set; }

        public DateTimeOffset StartTime { get; set; }

        public DateTimeOffset? EndTime { get; set; }

        public Guid GetFunctionInstanceId()
        {
            return Guid.Parse(this.FunctionInstanceId);
        }


        public string FunctionInstanceId
        {
            get; set;
        }

        public string FunctionName { get; set; }

        public string DisplayName { get; set; }
    }
}