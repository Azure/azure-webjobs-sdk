// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Logging.Internal
{
    /// <summary>
    /// Log function instance counts to a Azure Table
    /// </summary>
    public class CloudTableInstanceCountLogger : InstanceCountLoggerBase
    {
        private readonly CloudTable _instanceTable;
        private readonly string _containerName;

        private readonly int _containerSize;

        /// <summary>
        /// Rate at which to poll and call WriteEntry(). 
        /// </summary>
        public TimeSpan PollingInterval
        {
            get; set;
        }

        ///
        public CloudTableInstanceCountLogger(string containerName, CloudTable instanceTable, int containerSize)
        {
            this.PollingInterval = TimeSpan.FromSeconds(5);

            this._instanceTable = instanceTable;
            this._containerName = containerName;
            this._containerSize = containerSize;
        }

        /// 
        protected override async Task WriteEntry(long ticks, int value)
        {
            if (value == 0)
            {
                return;
            }
            var entity = new InstanceCountEntity(ticks, _containerName)
            {
                Count = value,
                Size = _containerSize,
                Duration = (int) this.PollingInterval.TotalMilliseconds
            };

            TableOperation opInsert = TableOperation.Insert(entity);
            await _instanceTable.ExecuteAsync(opInsert);
        }

        /// <summary>
        /// Poll, return the ticks. 
        /// </summary>
        /// <param name="token">cancellation token to interupt the poll. Don't throw when cancelled, just return early.</param>
        /// <returns>Tick counter after the poll.</returns>
        protected override async Task<long> WaitOnPoll(CancellationToken token)
        {
            try
            {
                await Task.Delay(PollingInterval, token);
            }
            catch (OperationCanceledException)
            {
                // Don't return yet. One last chance to flush 
            }

            return DateTime.UtcNow.Ticks;
        }
    }
}