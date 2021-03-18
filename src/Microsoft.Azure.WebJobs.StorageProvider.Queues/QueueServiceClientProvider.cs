// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Storage.Queues;
using Microsoft.Azure.WebJobs.Shared.StorageProvider;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.StorageProvider.Queues
{
    /// <summary>
    /// Provider to create QueueServiceClient objects with the registered <see cref="IConfiguration"/>
    /// </summary>
    public class QueueServiceClientProvider : StorageClientProvider<QueueServiceClient, QueueClientOptions>
    {
        public QueueServiceClientProvider(IConfiguration configuration, AzureComponentFactory componentFactory, AzureEventSourceLogForwarder logForwarder)
            : base(configuration, componentFactory, logForwarder) { }


        /// <inheritdoc/>
        protected override string ServiceUriSubDomain
        {
            get
            {
                return "queue";
            }
        }
    }
}
