// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs.Shared.StorageProvider;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.StorageProvider.Blobs
{
    /// <summary>
    /// Provider to create BlobServiceClient objects with the registered <see cref="IConfiguration"/>
    /// </summary>
    public class BlobServiceClientProvider : StorageClientProvider<BlobServiceClient, BlobClientOptions>
    {
        /// <summary>
        /// BlobStorageClientProvider
        /// </summary>
        /// <param name="configuration">Registered <see cref="IConfiguration"/></param>
        /// <param name="componentFactory">Registered <see cref="AzureComponentFactory"/></param>
        /// <param name="logForwarder">Registered <see cref="AzureEventSourceLogForwarder"/></param>
        public BlobServiceClientProvider(IConfiguration configuration, AzureComponentFactory componentFactory, AzureEventSourceLogForwarder logForwarder, ILogger<BlobServiceClient> logger)
            : base(configuration, componentFactory, logForwarder, logger) { }

        /// <inheritdoc/>
        protected override string ServiceUriSubDomain
        {
            get
            {
                return "blob";
            }
        }
    }
}
