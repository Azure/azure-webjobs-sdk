// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Host
{
    internal class BlobStorageConcurrencyStatusRepository : IConcurrencyStatusRepository
    {
        private readonly IHostIdProvider _hostIdProvider;
        private readonly ILogger _logger;
        private readonly IAzureBlobStorageProvider _blobStorageProvider;
        private BlobContainerClient? _blobContainerClient;

        public BlobStorageConcurrencyStatusRepository(IHostIdProvider hostIdProvider, ILoggerFactory loggerFactory, IAzureBlobStorageProvider azureStorageProvider)
        {
            _hostIdProvider = hostIdProvider;
            _logger = loggerFactory.CreateLogger(LogCategories.Concurrency);
            _blobStorageProvider = azureStorageProvider;
        }

        public async Task<HostConcurrencySnapshot?> ReadAsync(CancellationToken cancellationToken)
        {
            string blobPath = await GetBlobPathAsync(cancellationToken);

            try
            {
                BlobContainerClient? containerClient = await GetContainerClientAsync(cancellationToken);
                if (containerClient != null)
                {
                    BlobClient blobClient = containerClient.GetBlobClient(blobPath);
                    string content = await blobClient.DownloadTextAsync(cancellationToken: cancellationToken);

                    if (!string.IsNullOrEmpty(content))
                    {
                        var result = JsonConvert.DeserializeObject<HostConcurrencySnapshot>(content);
                        return result;
                    }
                }
            }
            catch (RequestFailedException exception) when (exception.Status == 404)
            {
                // we haven't recorded a status yet
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error reading snapshot blob {blobPath}");
                throw e;
            }

            return null;
        }

        public async Task WriteAsync(HostConcurrencySnapshot snapshot, CancellationToken cancellationToken)
        {
            string blobPath = await GetBlobPathAsync(cancellationToken);

            try
            {
                BlobContainerClient? containerClient = await GetContainerClientAsync(cancellationToken);
                if (containerClient != null)
                {
                    BlobClient blobClient = containerClient.GetBlobClient(blobPath);
                    var content = JsonConvert.SerializeObject(snapshot);
                    await blobClient.UploadTextAsync(content, overwrite: true, cancellationToken: cancellationToken);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error writing snapshot blob {blobPath}");
                throw e;
            }
        }

        internal async Task<BlobContainerClient?> GetContainerClientAsync(CancellationToken cancellationToken)
        {
            if (_blobContainerClient == null && _blobStorageProvider.TryCreateHostingBlobContainerClient(out _blobContainerClient))
            {
                await _blobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            }

            return _blobContainerClient;
        }

        internal async Task<string> GetBlobPathAsync(CancellationToken cancellationToken)
        {
            string hostId = await _hostIdProvider.GetHostIdAsync(cancellationToken);
            return $"concurrency/{hostId}/concurrencyStatus.json";
        }
    }
}
