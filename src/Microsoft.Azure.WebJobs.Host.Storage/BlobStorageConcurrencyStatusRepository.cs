// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
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
        private const string HostContainerName = "azure-webjobs-hosts";
        private readonly IHostIdProvider _hostIdProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private CloudBlobContainer? _blobContainer;

        public BlobStorageConcurrencyStatusRepository(IConfiguration configuration, IHostIdProvider hostIdProvider, ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            _hostIdProvider = hostIdProvider;
            _logger = loggerFactory.CreateLogger(LogCategories.Concurrency);
        }

        public async Task<HostConcurrencySnapshot?> ReadAsync(CancellationToken cancellationToken)
        {
            string blobPath = await GetBlobPathAsync(cancellationToken);

            try
            {
                CloudBlobContainer? container = await GetContainerAsync(cancellationToken);
                if (container != null)
                {
                    CloudBlockBlob blob = container.GetBlockBlobReference(blobPath);
                    string content = await blob.DownloadTextAsync(cancellationToken);
                    if (!string.IsNullOrEmpty(content))
                    {
                        var result = JsonConvert.DeserializeObject<HostConcurrencySnapshot>(content);
                        return result;
                    }
                }
            }
            catch (StorageException stex) when (stex.RequestInformation?.HttpStatusCode == 404)
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
                CloudBlobContainer? container = await GetContainerAsync(cancellationToken);
                if (container != null)
                {
                    CloudBlockBlob blob = container.GetBlockBlobReference(blobPath);

                    using (StreamWriter writer = new StreamWriter(await blob.OpenWriteAsync(cancellationToken)))
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

        internal async Task<CloudBlobContainer?> GetContainerAsync(CancellationToken cancellationToken)
        {
            if (_blobContainer == null)
            {
                string storageConnectionString = _configuration.GetWebJobsConnectionString(ConnectionStringNames.Storage);
                if (!string.IsNullOrEmpty(storageConnectionString) && CloudStorageAccount.TryParse(storageConnectionString, out CloudStorageAccount account))
                {
                    var client = account.CreateCloudBlobClient();
                    _blobContainer = client.GetContainerReference(HostContainerName);

                    await _blobContainer.CreateIfNotExistsAsync(cancellationToken);
                }
            }

            return _blobContainer;
        }

        internal async Task<string> GetBlobPathAsync(CancellationToken cancellationToken)
        {
            string hostId = await _hostIdProvider.GetHostIdAsync(cancellationToken);
            return $"concurrency/{hostId}/concurrencyStatus.json";
        }
    }
}
