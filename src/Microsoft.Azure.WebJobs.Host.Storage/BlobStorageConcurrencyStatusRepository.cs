// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Host
{
    internal class BlobStorageConcurrencyStatusRepository : IConcurrencyStatusRepository
    {
        private readonly IHostIdProvider _hostIdProvider;
        private readonly IAzureStorageProvider _azureStorageProvider;
        private readonly ILogger _logger;
        private BlobContainerClient? _blobContainerClient;

        public BlobStorageConcurrencyStatusRepository(IConfiguration configuration, IAzureStorageProvider azureStorageProvider, IHostIdProvider hostIdProvider, ILoggerFactory loggerFactory)
        {
            _hostIdProvider = hostIdProvider;
            _azureStorageProvider = azureStorageProvider;
            _logger = loggerFactory.CreateLogger(LogCategories.Concurrency);
        }

        public async Task<HostConcurrencySnapshot?> ReadAsync(CancellationToken cancellationToken)
        {
            string blobPath = await GetBlobPathAsync(cancellationToken);

            try
            {
                BlobContainerClient? container = await GetContainerAsync(cancellationToken);
                if (container != null)
                {
                    BlockBlobClient blob = container.GetBlockBlobClient(blobPath);
                    string? content = (await blob.DownloadContentAsync(cancellationToken)).Value?.Content?.ToString();
                    if (!string.IsNullOrEmpty(content))
                    {
                        var result = JsonConvert.DeserializeObject<HostConcurrencySnapshot>(content);
                        return result;
                    }
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
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
                BlobContainerClient? container = await GetContainerAsync(cancellationToken);
                if (container != null)
                {
                    BlockBlobClient blob = container.GetBlockBlobClient(blobPath);

                    using (StreamWriter writer = new StreamWriter(await blob.OpenWriteAsync(overwrite: true, cancellationToken: cancellationToken)))
                    {
                        var content = JsonConvert.SerializeObject(snapshot);
                        await writer.WriteAsync(content);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error writing snapshot blob {blobPath}");
                throw e;
            }
        }

        internal async Task<BlobContainerClient?> GetContainerAsync(CancellationToken cancellationToken)
        {
            if (_blobContainerClient == null)
            {
                try
                {
                    _blobContainerClient = _azureStorageProvider.GetWebJobsBlobContainerClient();
                    await _blobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Error getting BlobContainer for BlobStorageConcurrencyStatusRepository.");
                }
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
