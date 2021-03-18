// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs.StorageProvider.Blobs;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs
{
    public class HostStorageProvider
    {
        private IConfiguration _configuration;
        private BlobServiceClientProvider _blobServiceClientProvider;

        public IConfiguration Configuration
        {
            get
            {
                return _configuration;
            }
            set
            {
                _configuration = value;
            }
        }

        public HostStorageProvider(IConfiguration configuration, BlobServiceClientProvider blobServiceClientProvider)
        {
            _configuration = configuration;
            _blobServiceClientProvider = blobServiceClientProvider;
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
                client = new BlobServiceClient(connectionStringToUse);
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is ArgumentNullException || ex is InvalidOperationException)
            {
                client = default;
                return false;
            }
        }

        public virtual bool TryGetBlobServiceClientFromConnection(out BlobServiceClient client, string connection = null)
        {
            try
            {
                client = _blobServiceClientProvider.Get(connection);
                return true;
            }
            catch
            {
                client = default;
                return false;
            }
        }
    }
}
