// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Host.Storage
{
    /// <summary>
    /// Handles instantiating Azure storage clients
    /// </summary>
    public class AzureStorageProvider : IAzureStorageProvider
    {
        private IConfiguration _configuration;
        private BlobServiceClientProvider _blobServiceClientProvider;
        private IOptionsMonitor<JobHostInternalStorageOptions> _storageOptions;

        public AzureStorageProvider(IConfiguration configuration, BlobServiceClientProvider blobServiceClientProvider, IOptionsMonitor<JobHostInternalStorageOptions> options)
        {
            _configuration = configuration;
            _blobServiceClientProvider = blobServiceClientProvider;
            _storageOptions = options;
        }

        /// <summary>
        /// Checks whether the specified connection has an associated value
        /// </summary>
        /// <param name="connection">Connection to check</param>
        /// <returns>Whether the connection has an associated value or section</returns>
        public virtual bool ConnectionExists(string connection)
        {
            var section = _configuration.GetWebJobsConnectionStringSection(connection);
            return section != null && section.Exists();
        }

        /// <summary>
        /// Retrieves a BlobContainerClient for the reserved WebJobs container
        /// </summary>
        /// <returns>BlobContainerClient for WebJobs operations</returns>
        public BlobContainerClient GetWebJobsBlobContainerClient()
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

        /// <summary>
        /// Attempts to retrieve the BlobServiceClient from the specified connection
        /// </summary>
        /// <param name="client">client to instantiate</param>
        /// <param name="connection">connection to use</param>
        /// <returns>whether the attempt was successful</returns>
        public virtual bool TryGetBlobServiceClientFromConnection(out BlobServiceClient client, string connection)
        {
            var connectionToUse = connection ?? ConnectionStringNames.Storage;
            return _blobServiceClientProvider.TryGet(connectionToUse, _configuration, out client);
        }
    }
}
