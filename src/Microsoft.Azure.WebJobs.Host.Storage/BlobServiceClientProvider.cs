// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Azure.Core;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Storage
{
    /// <summary>
    /// Pass-through class to create a <see cref="BlobServiceClient"/> using <see cref="AzureComponentFactory"/>.
    /// </summary>
    public class BlobServiceClientProvider : StorageClientProvider<BlobServiceClient, BlobClientOptions>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BlobServiceClientProvider"/> class that uses the registered Azure services to create a BlobServiceClient.
        /// </summary>
        /// <param name="componentFactory">The Azure factory responsible for creating clients. <see cref="AzureComponentFactory"/></param>
        /// <param name="logForwarder">Log forwarder that forwards events to ILogger. <see cref="AzureEventSourceLogForwarder"/></param>
        /// <param name="logger">Instance of <see cref="ILogger{StorageClientProvider{TClient, TClientOptions}}"/> for logging events.</param>
        public BlobServiceClientProvider(AzureComponentFactory componentFactory, AzureEventSourceLogForwarder logForwarder, ILogger<BlobServiceClientProvider> logger)
            : base(componentFactory, logForwarder, logger) { }

        /// <inheritdoc/>
        protected override string ServiceUriSubDomain
        {
            get
            {
                return "blob";
            }
        }

        /// <inheritdoc/>
        protected override BlobServiceClient CreateClient(IConfiguration configuration, TokenCredential tokenCredential, BlobClientOptions options)
        {
            // If connection string is present, it will be honored first
            if (!IsConnectionStringPresent(configuration) && TryGetServiceUri(configuration, out Uri serviceUri))
            {
                return new BlobServiceClient(serviceUri, tokenCredential, options);
            }

            return base.CreateClient(configuration, tokenCredential, options);
        }
    }
}
