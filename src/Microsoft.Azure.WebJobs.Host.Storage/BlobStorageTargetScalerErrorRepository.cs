// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
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

        private const int MaxRetries = 3;
        internal static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(10);

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
                for (int attempt = 0; attempt < MaxRetries; attempt++)
                {
                    // Read current state with ETag
                    var (state, etag) = await ReadBlobWithETagAsync(cancellationToken);
                    var set = state?.Scalers ?? new HashSet<string>();
                    if (!set.Add(scalerUniqueId))
                    {
                        // Already present — still update the timestamp to keep it fresh
                    }

                    var newState = new TargetScalerErrorState
                    {
                        Scalers = set,
                        LastUpdated = DateTime.UtcNow
                    };

                    try
                    {
                        await WriteBlobAsync(newState, etag, cancellationToken);
                        return;
                    }
                    catch (RequestFailedException ex) when (ex.Status == 412 || ex.Status == 409)
                    {
                        // ETag mismatch — another instance wrote concurrently, retry
                    }
                }

                _logger.LogWarning("Failed to persist target scaler error state after {MaxRetries} attempts due to concurrent updates.", MaxRetries);
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
                var (state, _) = await ReadBlobWithETagAsync(cancellationToken);
                if (state?.LastUpdated != null && (DateTime.UtcNow - state.LastUpdated.Value) > DefaultTtl)
                {
                    // Data is stale — treat as empty so target scalers are re-evaluated
                    return new HashSet<string>();
                }
                return state?.Scalers ?? new HashSet<string>();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error reading target scaler error state.");
                return new HashSet<string>();
            }
        }

        private async Task<(TargetScalerErrorState?, ETag)> ReadBlobWithETagAsync(CancellationToken cancellationToken)
        {
            string blobPath = await GetBlobPathAsync(cancellationToken);

            try
            {
                BlobContainerClient? containerClient = await GetContainerClientAsync(cancellationToken);
                if (containerClient != null)
                {
                    BlobClient blobClient = containerClient.GetBlobClient(blobPath);
                    var response = await blobClient.DownloadAsync(cancellationToken: cancellationToken);

                    string content;
                    using (StreamReader reader = new StreamReader(response.Value.Content, true))
                    {
                        content = reader.ReadToEnd();
                    }

                    if (!string.IsNullOrEmpty(content))
                    {
                        var state = JsonConvert.DeserializeObject<TargetScalerErrorState>(content);
                        return (state, response.Value.Details.ETag);
                    }
                }
            }
            catch (RequestFailedException exception) when (exception.Status == 404)
            {
                // blob doesn't exist yet — no errors recorded
                return (null, default);
            }

            return (null, default);
        }

        private async Task WriteBlobAsync(TargetScalerErrorState state, ETag etag, CancellationToken cancellationToken)
        {
            string blobPath = await GetBlobPathAsync(cancellationToken);
            BlobContainerClient? containerClient = await GetContainerClientAsync(cancellationToken);
            if (containerClient != null)
            {
                BlobClient blobClient = containerClient.GetBlobClient(blobPath);
                var content = JsonConvert.SerializeObject(state);
                using (Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
                {
                    var options = new BlobUploadOptions();
                    if (etag != default)
                    {
                        // Existing blob — only write if it hasn't changed since we read it
                        options.Conditions = new BlobRequestConditions { IfMatch = etag };
                    }
                    else
                    {
                        // No blob exists yet — only create if it still doesn't exist
                        options.Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All };
                    }
                    await blobClient.UploadAsync(stream, options, cancellationToken);
                }
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

        internal class TargetScalerErrorState
        {
            [JsonProperty("scalers")]
            public HashSet<string> Scalers { get; set; } = new HashSet<string>();

            [JsonProperty("lastUpdated")]
            public DateTime? LastUpdated { get; set; }
        }
    }
}
