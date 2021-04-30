// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Azure.Core;
using Azure.Storage.Queues;
using Microsoft.Azure.WebJobs.StorageProvider.Common;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.StorageProvider.Queues
{
    /// <summary>
    /// Provider to create QueueServiceClient objects
    /// </summary>
    public class QueueServiceClientProvider : StorageClientProvider<QueueServiceClient, QueueClientOptions>
    {
        public QueueServiceClientProvider(IConfiguration configuration, AzureComponentFactory componentFactory, AzureEventSourceLogForwarder logForwarder, ILogger<QueueServiceClient> logger)
            : base(configuration, componentFactory, logForwarder, logger) { }

        /// <inheritdoc/>
        protected override QueueServiceClient CreateClient(IConfiguration configuration, TokenCredential tokenCredential, QueueClientOptions options)
        {
            // If connection string is present, it will be honored first
            if (!IsConnectionStringPresent(configuration) && TryGetServiceUri(configuration, out Uri serviceUri))
            {
                return new QueueServiceClient(serviceUri, tokenCredential, options);
            }

            return base.CreateClient(configuration, tokenCredential, options);
        }

        /// <inheritdoc/>
        protected override QueueServiceClient CreateClient(string connectionString)
        {
            return new QueueServiceClient(connectionString);
        }

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
