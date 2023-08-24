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
    /// Certain configuration settings are used to instantiate the clients. These include settings
    /// necessary to construct the Azure service URIs and settings to specify credential related
    /// information (i.e. clientId, tenantId, etc. where applicable).
    /// <see cref="Microsoft.Extensions.Azure.ClientFactory"/> is where a bulk of the
    /// <see cref="IConfiguration"/> source is read.
    /// This implementation adds extra configuration by using <see cref="StorageServiceUriOptions"/> to bind to
    /// a particular <see cref="IConfigurationSection"/>.
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

        public virtual bool TryCreateHostingBlobContainerClient(out BlobContainerClient blobContainerClient)
        {
            if (_storageOptions?.CurrentValue.InternalSasBlobContainer != null)
            {
                blobContainerClient = new BlobContainerClient(new Uri(_storageOptions.CurrentValue.InternalSasBlobContainer));
                _logger.LogDebug($"Using storage account {blobContainerClient.AccountName} and container {blobContainerClient.Name} for hosting BlobContainerClient.");
                return true;
            }

            if (!TryCreateBlobServiceClientFromConnection(ConnectionStringNames.Storage, out BlobServiceClient blobServiceClient))
            {
                _logger.LogDebug($"Could not create BlobContainerClient using Connection: {ConnectionStringNames.Storage}");
                blobContainerClient = default;
                return false;
            }

            blobContainerClient = blobServiceClient.GetBlobContainerClient(HostContainerNames.Hosts);
            return true;
        }

        public virtual bool TryCreateBlobServiceClientFromConnection(string connection, out BlobServiceClient client)
        {
            var connectionToUse = connection ?? ConnectionStringNames.Storage;

            try
            {
                client = _blobServiceClientProvider.Create(connectionToUse, _configuration);
                return true;
            }
            catch (Exception e)
            {
                _logger.LogDebug($"Could not create BlobServiceClient. Exception: {e}");
                client = default;
                return false;
            }
        }
    }
}
