// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Host.TestCommon
{
    class TestAzureStorageProvider : IAzureStorageProvider
    {
        private IConfiguration _configuration;
        private BlobServiceClientProvider _blobServiceClientProvider;
        private IOptionsMonitor<JobHostInternalStorageOptions> _storageOptions;

        public TestAzureStorageProvider(IConfiguration configuration, BlobServiceClientProvider blobServiceClientProvider, IOptionsMonitor<JobHostInternalStorageOptions> options)
        {
            _configuration = configuration;
            _blobServiceClientProvider = blobServiceClientProvider;
            _storageOptions = options;
        }

        public bool ConnectionExists(string connection)
        {
            return _configuration.GetWebJobsConnectionStringSection(connection).Exists();
        }

        public BlobContainerClient GetWebJobsBlobContainerClient()
        {
            if (_storageOptions?.CurrentValue.InternalSasBlobContainer != null)
            {
                return new BlobContainerClient(new Uri(_storageOptions.CurrentValue.InternalSasBlobContainer));
            }

            if (!TryGetBlobServiceClientFromConnection(out BlobServiceClient blobServiceClient, ConnectionStringNames.Storage))
            {
                throw new InvalidOperationException($"Could not create BlobContainerClient in TestAzureStorageProvider using Connection: {ConnectionStringNames.Storage}");
            }

            return blobServiceClient.GetBlobContainerClient(HostContainerNames.Hosts);
        }

        public bool TryGetBlobServiceClientFromConnection(out BlobServiceClient client, string connection)
        {
            return _blobServiceClientProvider.TryGet(connection, _configuration, out client);
        }
    }
}
