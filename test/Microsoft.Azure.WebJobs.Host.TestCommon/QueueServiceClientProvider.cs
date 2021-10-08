// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure.Storage.Queues;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Extensions.Azure;

namespace Microsoft.Azure.WebJobs.Host.TestCommon
{
    /// <summary>
    /// Provider to create QueueServiceClient objects. This is used only for tests and is intended to only honor connection strings.
    /// </summary>
    internal class QueueServiceClientProvider : StorageClientProvider<QueueServiceClient, QueueClientOptions>
    {
        public QueueServiceClientProvider(AzureComponentFactory componentFactory, AzureEventSourceLogForwarder logForwarder)
            : base(componentFactory, logForwarder) { }
    }
}
