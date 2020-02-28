// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Queue;

namespace Microsoft.Azure.WebJobs.Host.Queues
{
    internal static class StorageQueueExtensions
    {
        public static async Task AddMessageAndCreateIfNotExistsAsync(this CloudQueue queue,
            CloudQueueMessage message, CancellationToken cancellationToken)
        {
            if (queue == null)
            {
                throw new ArgumentNullException("queue");
            }

            bool isQueueNotFoundException = false;

            try
            {
                await queue.AddMessageAsync(message, cancellationToken);
                return;
            }
            catch (StorageException exception)
            {
                if (!exception.IsNotFoundQueueNotFound())
                {
                    ExceptionDispatchInfo.Capture(exception).Throw();
                }

                isQueueNotFoundException = true;
            }

            Debug.Assert(isQueueNotFoundException);
            await queue.CreateIfNotExistsAsync(cancellationToken);
            await queue.AddMessageAsync(message, cancellationToken);
        }
    }
}
