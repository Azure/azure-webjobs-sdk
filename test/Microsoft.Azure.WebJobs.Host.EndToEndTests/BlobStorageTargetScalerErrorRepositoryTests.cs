// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Xunit;
using static Microsoft.Azure.WebJobs.Host.BlobStorageTargetScalerErrorRepository;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    [Trait(TestTraits.CategoryTraitName, TestTraits.ScaleMonitoring)]
    public class BlobStorageTargetScalerErrorRepositoryTests
    {
        private const string TestHostId = "test123";
        private readonly BlobStorageTargetScalerErrorRepository _repository;
        private readonly LoggerFactory _loggerFactory;
        private readonly TestLoggerProvider _loggerProvider;
        private readonly Mock<IHostIdProvider> _mockHostIdProvider;

        public BlobStorageTargetScalerErrorRepositoryTests()
        {
            _loggerFactory = new LoggerFactory();
            _loggerProvider = new TestLoggerProvider();
            _loggerFactory.AddProvider(_loggerProvider);

            _mockHostIdProvider = new Mock<IHostIdProvider>(MockBehavior.Strict);
            _mockHostIdProvider.Setup(p => p.GetHostIdAsync(CancellationToken.None)).ReturnsAsync(TestHostId);

            _repository = new BlobStorageTargetScalerErrorRepository(_mockHostIdProvider.Object, _loggerFactory, TestHelpers.GetTestAzureBlobStorageProvider());
        }

        [Fact]
        public async Task GetBlobPathAsync_ReturnsExpectedPath()
        {
            string path = await _repository.GetBlobPathAsync(CancellationToken.None);

            Assert.Equal($"scale/{TestHostId}/targetScalersInError.json", path);
        }

        [Fact]
        public async Task AddAsync_WritesExpectedBlob()
        {
            await DeleteTestBlobsAsync();

            // Verify blob doesn't exist
            var path = await _repository.GetBlobPathAsync(CancellationToken.None);
            BlobContainerClient blobContainerClient = await _repository.GetContainerClientAsync(CancellationToken.None);
            BlobClient blobClient = blobContainerClient.GetBlobClient(path);
            bool exists = await blobClient.ExistsAsync();
            Assert.False(exists);

            // Add a scaler error
            await _repository.AddAsync("scaler-a", CancellationToken.None);

            // Verify blob was created with correct content
            exists = await blobClient.ExistsAsync();
            Assert.True(exists);

            string content = await blobClient.DownloadTextAsync();
            var state = JsonConvert.DeserializeObject<TargetScalerErrorState>(content);
            Assert.Single(state.Scalers);
            Assert.Contains("scaler-a", state.Scalers);
            Assert.NotNull(state.LastUpdated);

            // Add another and verify both are present
            await _repository.AddAsync("scaler-b", CancellationToken.None);
            content = await blobClient.DownloadTextAsync();
            state = JsonConvert.DeserializeObject<TargetScalerErrorState>(content);
            Assert.Equal(2, state.Scalers.Count);
            Assert.Contains("scaler-a", state.Scalers);
            Assert.Contains("scaler-b", state.Scalers);
        }

        [Fact]
        public async Task GetAsync_ReadsExpectedBlob()
        {
            await DeleteTestBlobsAsync();

            // Write a blob directly using the new state format
            string path = await _repository.GetBlobPathAsync(CancellationToken.None);
            BlobContainerClient blobContainerClient = await _repository.GetContainerClientAsync(CancellationToken.None);
            BlobClient blobClient = blobContainerClient.GetBlobClient(path);

            var testState = new TargetScalerErrorState
            {
                Scalers = new HashSet<string> { "scaler-x", "scaler-y" },
                LastUpdated = DateTime.UtcNow
            };
            string content = JsonConvert.SerializeObject(testState);
            await blobClient.UploadTextAsync(content, overwrite: true);

            // Read via repository
            var result = await _repository.GetAsync(CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Contains("scaler-x", result);
            Assert.Contains("scaler-y", result);
        }

        [Fact]
        public async Task GetAsync_NoBlob_ReturnsEmpty()
        {
            await DeleteTestBlobsAsync();

            var result = await _repository.GetAsync(CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAsync_StaleData_ReturnsEmpty()
        {
            await DeleteTestBlobsAsync();

            // Write a blob with an old timestamp (beyond the TTL)
            string path = await _repository.GetBlobPathAsync(CancellationToken.None);
            BlobContainerClient blobContainerClient = await _repository.GetContainerClientAsync(CancellationToken.None);
            BlobClient blobClient = blobContainerClient.GetBlobClient(path);

            var staleState = new TargetScalerErrorState
            {
                Scalers = new HashSet<string> { "scaler-stale" },
                LastUpdated = DateTime.UtcNow - BlobStorageTargetScalerErrorRepository.DefaultTtl - TimeSpan.FromMinutes(1)
            };
            string content = JsonConvert.SerializeObject(staleState);
            await blobClient.UploadTextAsync(content, overwrite: true);

            // GetAsync should return empty because data is beyond TTL
            var result = await _repository.GetAsync(CancellationToken.None);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAsync_FreshData_ReturnsScalers()
        {
            await DeleteTestBlobsAsync();

            // Write via AddAsync (writes current timestamp)
            await _repository.AddAsync("scaler-fresh", CancellationToken.None);

            // GetAsync should return the scaler because data is fresh
            var result = await _repository.GetAsync(CancellationToken.None);
            Assert.Single(result);
            Assert.Contains("scaler-fresh", result);
        }

        [Fact]
        public async Task NoStorageConnection_HandledGracefully()
        {
            var mockBlobStorageProvider = new Mock<IAzureBlobStorageProvider>(MockBehavior.Strict);
            BlobContainerClient blobContainerClient = null;
            mockBlobStorageProvider.Setup(p => p.TryCreateHostingBlobContainerClient(out blobContainerClient)).Returns(false);
            var localRepository = new BlobStorageTargetScalerErrorRepository(_mockHostIdProvider.Object, _loggerFactory, mockBlobStorageProvider.Object);

            var container = await localRepository.GetContainerClientAsync(CancellationToken.None);
            Assert.Null(container);

            // These should not throw — they handle null container gracefully
            await localRepository.AddAsync("scaler-a", CancellationToken.None);

            var result = await localRepository.GetAsync(CancellationToken.None);
            Assert.Empty(result);
        }

        private async Task DeleteTestBlobsAsync()
        {
            BlobContainerClient blobContainerClient = await _repository.GetContainerClientAsync(CancellationToken.None);
            var blobItems = blobContainerClient.GetBlobsByHierarchyAsync(prefix: $"scale/{TestHostId}");
            await foreach (var blob in blobItems)
            {
                BlobClient blobClient = blobContainerClient.GetBlobClient(blob.Blob.Name);
                await blobClient.DeleteAsync();
            }
        }
    }
}
