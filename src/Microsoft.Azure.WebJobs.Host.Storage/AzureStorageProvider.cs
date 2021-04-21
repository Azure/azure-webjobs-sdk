// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.StorageProvider.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// This serves as a placeholder for a concrete implementation of a storage abstraction.
    /// This StorageProvider provides a wrapper to align all uses of storage by the Functions Host
    /// </summary>
    internal class AzureStorageProvider : IAzureStorageProvider
    {
        private readonly BlobServiceClientProvider _blobServiceClientProvider;
        private readonly IConfiguration _configuration;
        private JobHostInternalStorageOptions _storageOptions;

        public AzureStorageProvider(IConfiguration configuration, BlobServiceClientProvider blobServiceClientProvider, IOptions<JobHostInternalStorageOptions> options)
        {
            _blobServiceClientProvider = blobServiceClientProvider;
            _configuration = configuration;
            _storageOptions = options?.Value;
        }

        /// <summary>
        /// Try create BlobServiceClient
        /// SDK parse connection string method can throw exceptions: https://github.com/Azure/azure-sdk-for-net/blob/master/sdk/storage/Azure.Storage.Common/src/Shared/StorageConnectionString.cs#L238
        /// </summary>
        /// <param name="client">client to instantiate</param>
        /// <param name="connectionString">connection string to use</param>
        /// <returns>successful client creation</returns>
        public virtual bool TryGetBlobServiceClientFromConnectionString(out BlobServiceClient client, string connectionString = null)
        {
            try
            {
                var connectionStringToUse = connectionString ?? _configuration.GetWebJobsConnectionString(ConnectionStringNames.Storage);
                _blobServiceClientProvider.TryGetFromConnectionString(connectionString, out client);
                return true;
            }
            catch
            {
                client = default(BlobServiceClient);
                return false;
            }
        }

        /// <summary>
        /// Try create BlobServiceClient
        /// </summary>
        /// <param name="client">client to instantiate</param>
        /// <param name="connection">Name of the connection to use</param>
        /// <returns>successful client creation</returns>
        public virtual bool TryGetBlobServiceClientFromConnection(out BlobServiceClient client, string connection = null)
        {
            try
            {
                client = _blobServiceClientProvider.Get(connection);
                return true;
            }
            catch
            {
                client = default(BlobServiceClient);
                return false;
            }
        }

        public virtual BlobContainerClient GetBlobContainerClient()
        {
            if (_storageOptions?.InternalSasBlobContainer != null)
            {
                return new BlobContainerClient(new Uri(_storageOptions.InternalSasBlobContainer));
            }

            if (!TryGetBlobServiceClientFromConnection(out BlobServiceClient blobServiceClient, ConnectionStringNames.Storage))
            {
                throw new InvalidOperationException("Could not create BlobServiceClient for the BlobLeaseDistributedLockManager");
            }

            if (_storageOptions?.InternalContainerName != null)
            {
                return blobServiceClient.GetBlobContainerClient(_storageOptions.InternalContainerName);
            }
            else
            {
                return blobServiceClient.GetBlobContainerClient(HostContainerNames.Hosts);
            }
        }
    }
}
