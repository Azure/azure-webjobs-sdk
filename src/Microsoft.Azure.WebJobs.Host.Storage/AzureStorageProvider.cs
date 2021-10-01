// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Host.Storage
{
    /// <summary>
    /// Handles instantiating Azure storage clients from an <see cref="IConfiguration"/> source.
    /// </summary>
    public class AzureStorageProvider : IAzureStorageProvider
    {
        private BlobServiceClientProvider _blobServiceClientProvider;
        private IOptionsMonitor<JobHostInternalStorageOptions> _storageOptions;

        /// <summary>
        /// <see cref="IConfiguration"/> instance to retrieve connection values.
        /// </summary>
        protected IConfiguration Configuration { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureStorageProvider"/> class.
        /// </summary>
        /// <param name="configuration"><see cref="IConfiguration"/> object to use for retrieving connection values.</param>
        /// <param name="blobServiceClientProvider"><see cref="BlobServiceClientProvider"/> to instantiate Azure Blob-related clients.</param>
        /// <param name="options">Options to define default Storage Blob containers for internal WebJobs operations.</param>
        public AzureStorageProvider(IConfiguration configuration, BlobServiceClientProvider blobServiceClientProvider, IOptionsMonitor<JobHostInternalStorageOptions> options)
        {
            Configuration = configuration;
            _blobServiceClientProvider = blobServiceClientProvider;
            _storageOptions = options;
        }

        /// <inheritdoc/>
        public virtual bool ConnectionExists(string connection)
        {
            var section = Configuration.GetWebJobsConnectionStringSection(connection);
            return section != null && section.Exists();
        }

        /// <inheritdoc/>
        public virtual BlobContainerClient GetWebJobsBlobContainerClient()
        {
            if (_storageOptions?.CurrentValue.InternalSasBlobContainer != null)
            {
                return new BlobContainerClient(new Uri(_storageOptions.CurrentValue.InternalSasBlobContainer));
            }

            if (!TryGetBlobServiceClientFromConnection(out BlobServiceClient blobServiceClient, ConnectionStringNames.Storage))
            {
                throw new InvalidOperationException($"Could not create BlobContainerClient in AzureStorageProvider using Connection: {ConnectionStringNames.Storage}");
            }

            return blobServiceClient.GetBlobContainerClient(HostContainerNames.Hosts);
        }

        /// <inheritdoc/>
        public virtual bool TryGetBlobServiceClientFromConnection(out BlobServiceClient client, string connection)
        {
            var connectionToUse = connection ?? ConnectionStringNames.Storage;
            return _blobServiceClientProvider.TryCreate(connectionToUse, Configuration, out client);
        }
    }
}
