// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Dispatch;
using Microsoft.Azure.Storage.Blob;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Listeners
{
    internal class SharedQueueBlobListener : IListener
    {
        public SharedQueueBlobListener(ListenerFactoryContext context, StorageAccount dataAccount)
        {
            // register SharedQueueBlobHandler handler
            context.GetDispatchQueue(new SharedQueueBlobHandler(context.Executor, dataAccount));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public void Dispose()
        {
        }

        public void Cancel()
        {
        }

        internal class SharedQueueBlobHandler : IMessageHandler
        {
            private ITriggeredFunctionExecutor _executor;
            private CloudBlobClient _blobClient;

            public SharedQueueBlobHandler(ITriggeredFunctionExecutor executor, StorageAccount dataAccount)
            {
                _executor = executor;
                _blobClient = dataAccount.CreateCloudBlobClient();
            }
            public async Task<FunctionResult> TryExecuteAsync(JObject data, CancellationToken cancellationToken)
            {
                // Both Event Grid schema and Cloud Event schema define blob uri in ["data"]["url"]
                ICloudBlob blob = await _blobClient.GetBlobReferenceFromServerAsync(new Uri(data["data"]["url"].ToString()));

                TriggeredFunctionData input = new TriggeredFunctionData
                {
                    TriggerValue = blob
                };
                return await _executor.TryExecuteAsync(input, cancellationToken);
            }
        }
    }
}
