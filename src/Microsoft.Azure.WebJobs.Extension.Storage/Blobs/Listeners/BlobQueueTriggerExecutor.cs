// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Listeners
{
    internal class BlobQueueTriggerExecutor : ITriggerExecutor<CloudQueueMessage>
    {
        private readonly IBlobETagReader _eTagReader;
        private readonly IBlobCausalityReader _causalityReader;
        private readonly IBlobWrittenWatcher _blobWrittenWatcher;
        private readonly ConcurrentDictionary<string, BlobQueueRegistration> _registrations;

        public BlobQueueTriggerExecutor(IBlobWrittenWatcher blobWrittenWatcher)
            : this(BlobETagReader.Instance, BlobCausalityReader.Instance, blobWrittenWatcher)
        {
        }

        public BlobQueueTriggerExecutor(IBlobETagReader eTagReader,
            IBlobCausalityReader causalityReader, IBlobWrittenWatcher blobWrittenWatcher)
        {
            _eTagReader = eTagReader;
            _causalityReader = causalityReader;
            _blobWrittenWatcher = blobWrittenWatcher;
            _registrations = new ConcurrentDictionary<string, BlobQueueRegistration>();
        }

        public bool TryGetRegistration(string functionId, out BlobQueueRegistration registration)
        {
            return _registrations.TryGetValue(functionId, out registration);
        }

        public void Register(string functionId, BlobQueueRegistration registration)
        {
            _registrations.AddOrUpdate(functionId, registration, (i1, i2) => registration);
        }

        public async Task<FunctionResult> ExecuteAsync(CloudQueueMessage value, CancellationToken cancellationToken)
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
            FunctionResult successResult = new FunctionResult(true);
            BlobQueueRegistration registration;
            if (!_registrations.TryGetValue(functionId, out registration))
            {
                return successResult;
            }

            var container = registration.BlobClient.GetContainerReference(message.ContainerName);
            string blobName = message.BlobName;

            ICloudBlob blob;

            try
            {
                blob = await container.GetBlobReferenceFromServerAsync(blobName);
            }
            catch (StorageException exception) when (exception.IsNotFound() || exception.IsOk())
            {
                // If the blob no longer exists, just ignore this message.
                return successResult;                
            }

            // Ensure the blob still exists with the same ETag.
            string possibleETag = blob.Properties.ETag; // set since we fetched from server

            // If the blob still exists but the ETag is different, delete the message but do a fast path notification.
            if (!String.Equals(message.ETag, possibleETag, StringComparison.Ordinal))
            {
                _blobWrittenWatcher.Notify(blob);
                return successResult;
            }

            // If the blob still exists and its ETag is still valid, execute.
            // Note: it's possible the blob could change/be deleted between now and when the function executes.
            Guid? parentId = await _causalityReader.GetWriterAsync(blob, cancellationToken);
            TriggeredFunctionData input = new TriggeredFunctionData
            {
                ParentId = parentId,
                TriggerValue = blob
            };

            return await registration.Executor.TryExecuteAsync(input, cancellationToken);
        }
    }
}
