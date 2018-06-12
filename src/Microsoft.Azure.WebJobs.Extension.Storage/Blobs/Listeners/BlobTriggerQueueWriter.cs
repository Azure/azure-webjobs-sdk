﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using WebJobs.Extension.Storage;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Listeners
{
    internal class BlobTriggerQueueWriter : IBlobTriggerQueueWriter
    {
        private readonly CloudQueue _queue;
        private readonly IMessageEnqueuedWatcher _watcher;

        public BlobTriggerQueueWriter(CloudQueue queue, IMessageEnqueuedWatcher watcher)
        {
            _queue = queue;
            Debug.Assert(watcher != null);
            _watcher = watcher;
        }

        public async Task EnqueueAsync(BlobTriggerMessage message, CancellationToken cancellationToken)
        {
            string contents = JsonConvert.SerializeObject(message, JsonSerialization.Settings);
            await _queue.AddMessageAndCreateIfNotExistsAsync(new CloudQueueMessage(contents), cancellationToken);
            _watcher.Notify(_queue.Name);
        }
    }
}
