// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
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
    internal class BlobStorageTargetScalerErrorRepository : ITargetScalerErrorRepository
    {
        private readonly IHostIdProvider _hostIdProvider;
        private readonly ILogger _logger;
        private readonly IAzureBlobStorageProvider _blobStorageProvider;
        private BlobContainerClient? _blobContainerClient;

        public BlobStorageTargetScalerErrorRepository(IHostIdProvider hostIdProvider, ILoggerFactory loggerFactory, IAzureBlobStorageProvider azureStorageProvider)
        {
            _hostIdProvider = hostIdProvider;
            _logger = loggerFactory.CreateLogger(LogCategories.Scale);
            _blobStorageProvider = azureStorageProvider;
        }

        public async Task AddAsync(string scalerUniqueId, CancellationToken cancellationToken)
        {
            try
            {
                // Read current set, add the new entry, and write back
                var scalersInError = await ReadBlobAsync(cancellationToken) ?? new HashSet<string>();
                if (scalersInError.Add(scalerUniqueId))
                {
                    await WriteBlobAsync(scalersInError, cancellationToken);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error persisting target scaler error state.");
            }
        }

        public async Task<ISet<string>> GetAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await ReadBlobAsync(cancellationToken) ?? new HashSet<string>();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error reading target scaler error state.");
                return new HashSet<string>();
            }
        }

        public async Task ClearAsync(CancellationToken cancellationToken)
        {
            try
            {
                string blobPath = await GetBlobPathAsync(cancellationToken);
                BlobContainerClient? containerClient = await GetContainerClientAsync(cancellationToken);
                if (containerClient != null)
                {
                    BlobClient blobClient = containerClient.GetBlobClient(blobPath);
                    await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error clearing target scaler error state.");
            }
        }

        private async Task<HashSet<string>?> ReadBlobAsync(CancellationToken cancellationToken)
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
                        return JsonConvert.DeserializeObject<HashSet<string>>(content);
                    }
                }
            }
            catch (RequestFailedException exception) when (exception.Status == 404)
            {
                // blob doesn't exist yet — no errors recorded
                return null;
            }

            return null;
        }

        private async Task WriteBlobAsync(HashSet<string> scalersInError, CancellationToken cancellationToken)
        {
            string blobPath = await GetBlobPathAsync(cancellationToken);
            BlobContainerClient? containerClient = await GetContainerClientAsync(cancellationToken);
            if (containerClient != null)
            {
                BlobClient blobClient = containerClient.GetBlobClient(blobPath);
                var content = JsonConvert.SerializeObject(scalersInError);
                await blobClient.UploadTextAsync(content, overwrite: true, cancellationToken: cancellationToken);
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
            return $"scale/{hostId}/targetScalersInError.json";
        }
    }
}
