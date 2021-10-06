// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Host.Storage
{
    /// <summary>
    /// Handles instantiating Azure storage clients from an <see cref="IConfiguration"/> source.
    /// </summary>
    internal class AzureStorageProvider : IAzureBlobStorageProvider
    {
        private readonly BlobServiceClientProvider _blobServiceClientProvider;
        private readonly ILogger<AzureStorageProvider> _logger;
        private readonly IConfiguration _configuration;
        private readonly IOptionsMonitor<JobHostInternalStorageOptions> _storageOptions;

        public AzureStorageProvider(IConfiguration configuration, IOptionsMonitor<JobHostInternalStorageOptions> options, ILogger<AzureStorageProvider> logger, AzureComponentFactory componentFactory, AzureEventSourceLogForwarder logForwarder)
        {
            _configuration = configuration;
            _storageOptions = options;
            _logger = logger;

            _blobServiceClientProvider = new BlobServiceClientProvider(componentFactory, logForwarder);
        }

        public virtual BlobContainerClient GetWebJobsBlobContainerClient()
        {
            if (_storageOptions?.CurrentValue.InternalSasBlobContainer != null)
            {
                return new BlobContainerClient(new Uri(_storageOptions.CurrentValue.InternalSasBlobContainer));
            }

            if (!TryGetBlobServiceClientFromConnection(ConnectionStringNames.Storage, out BlobServiceClient blobServiceClient))
            {
                throw new InvalidOperationException($"Could not create BlobContainerClient in AzureStorageProvider using Connection: {ConnectionStringNames.Storage}");
            }

            return blobServiceClient.GetBlobContainerClient(HostContainerNames.Hosts);
        }

        public virtual bool TryGetBlobServiceClientFromConnection(string connection, out BlobServiceClient client)
        {
            var connectionToUse = connection ?? ConnectionStringNames.Storage;

            try
            {
                client = _blobServiceClientProvider.Create(connectionToUse, _configuration);
                return true;
            }
            catch (Exception e)
            {
                _logger.LogDebug($"Could not create BlobServiceClient in AzureStorageProvider. Exception: {e}");
                client = default;
                return false;
            }
        }
    }
}
