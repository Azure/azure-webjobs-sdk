// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.Azure.WebJobs.Logging.Internal
{
    /// <summary>
    /// Entity for logging function instance counts
    /// </summary>
    public class InstanceCountEntity : TableEntity
    {
        const string PartitionKeyFormat = TableScheme.InstanceCountPK;
        const string RowKeyPrefix = "{0:D20}-";
        const string RowKeyFormat = "{0:D20}-{1}-{2}"; // timestamp ticks, container name, salt

        // Have a salt value for writing to avoid collisions since timeBucket is not gauranteed to be unique
        // when many functions are quickly run within a single time tick. 
        static int _salt;

        ///
        public InstanceCountEntity()
        {
        }

        // from rowKey
        ///
        public long GetTicks()
        {
            var time = TableScheme.Get1stTerm(this.RowKey);
            long ticks = long.Parse(time, CultureInfo.InvariantCulture);
            return ticks;
        }

        ///
        public InstanceCountEntity(long ticks, string containerName)
        {
            int salt = Interlocked.Increment(ref _salt);

            this.PartitionKey = PartitionKeyFormat;
            this.RowKey = string.Format(CultureInfo.InvariantCulture, RowKeyFormat, ticks, containerName, salt);
        }

        ///
        public static TableQuery<InstanceCountEntity> GetQuery(DateTime startTime, DateTime endTime)
        {
            if (startTime > endTime)
            {
                throw new InvalidOperationException("Start time must be less than or equal to end time");
            }
            string rowKeyStart = string.Format(CultureInfo.InvariantCulture, RowKeyPrefix, startTime.Ticks);
            string rowKeyEnd = string.Format(CultureInfo.InvariantCulture, RowKeyPrefix, endTime.Ticks);

            var query = TableScheme.GetRowsInRange<InstanceCountEntity>(PartitionKeyFormat, rowKeyStart, rowKeyEnd);
            return query;
        }

        /// <summary>
        /// Number of inidividual instances run in this period
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Size of the machine that these instances ran on.  
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// Polling duration in MS. 
        /// </summary>
        public int Duration { get; set; }
    }
}