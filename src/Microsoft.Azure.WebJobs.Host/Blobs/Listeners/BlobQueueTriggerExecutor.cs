﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.Storage.Queue;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Listeners
{
    internal class BlobQueueTriggerExecutor : ITriggerExecutor<IStorageQueueMessage>
    {
        private readonly IStorageBlobClient _client;
        private readonly IBlobETagReader _eTagReader;
        private readonly IBlobCausalityReader _causalityReader;
        private readonly ITriggeredFunctionExecutor<IStorageBlob> _innerExecutor;
        private readonly IBlobWrittenWatcher _blobWrittenWatcher;
        private readonly ConcurrentDictionary<string, ITriggeredFunctionExecutor<IStorageBlob>> _registrations;

        public BlobQueueTriggerExecutor(IStorageBlobClient client, ITriggeredFunctionExecutor<IStorageBlob> innerExecutor, IBlobWrittenWatcher blobWrittenWatcher)
            : this(client, BlobETagReader.Instance, BlobCausalityReader.Instance, innerExecutor, blobWrittenWatcher)
        {
        }

        public BlobQueueTriggerExecutor(IStorageBlobClient client, IBlobETagReader eTagReader,
            IBlobCausalityReader causalityReader, ITriggeredFunctionExecutor<IStorageBlob> innerExecutor, IBlobWrittenWatcher blobWrittenWatcher)
        {
            _client = client;
            _eTagReader = eTagReader;
            _causalityReader = causalityReader;
            _innerExecutor = innerExecutor;
            _blobWrittenWatcher = blobWrittenWatcher;
            _registrations = new ConcurrentDictionary<string, ITriggeredFunctionExecutor<IStorageBlob>>();
        }

        public void Register(string functionId, ITriggeredFunctionExecutor<IStorageBlob> executor)
        {
            _registrations.AddOrUpdate(functionId, executor, (i1, i2) => executor);
        }

        public async Task<bool> ExecuteAsync(IStorageQueueMessage value, CancellationToken cancellationToken)
        {
            BlobTriggerMessage message = JsonConvert.DeserializeObject<BlobTriggerMessage>(value.AsString, JsonSerialization.Settings);

            if (message == null)
            {
                throw new InvalidOperationException("Invalid blob trigger message.");
            }

            string functionId = message.FunctionId;

            if (functionId == null)
            {
                throw new InvalidOperationException("Invalid function ID.");
            }

            // Ensure that the function ID is still valid. Otherwise, ignore this message.
            ITriggeredFunctionExecutor<IStorageBlob> executor;
            if (!_registrations.TryGetValue(functionId, out executor))
            {
                return true;
            }

            IStorageBlobContainer container = _client.GetContainerReference(message.ContainerName);
            string blobName = message.BlobName;

            IStorageBlob blob;
            
            switch (message.BlobType)
            {
                case StorageBlobType.PageBlob:
                    blob = container.GetPageBlobReference(blobName);
                    break;
                case StorageBlobType.BlockBlob:
                default:
                    blob = container.GetBlockBlobReference(blobName);
                    break;
            }

            // Ensure the blob still exists with the same ETag.
            string possibleETag = await _eTagReader.GetETagAsync(blob, cancellationToken);

            if (possibleETag == null)
            {
                // If the blob no longer exists, just ignore this message.
                return true;
            }

            // If the blob still exists but the ETag is different, delete the message but do a fast path notification.
            if (!String.Equals(message.ETag, possibleETag, StringComparison.Ordinal))
            {
                _blobWrittenWatcher.Notify(blob);
                return true;
            }

            //// If the blob still exists and its ETag is still valid, execute.
            //// Note: it's possible the blob could change/be deleted between now and when the function executes.
            Guid? parentId = await _causalityReader.GetWriterAsync(blob, cancellationToken);
            return await executor.TryExecuteAsync(parentId, blob, cancellationToken);
        }
    }
}
