// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

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
            var result = JsonConvert.DeserializeObject<HashSet<string>>(content);
            Assert.Single(result);
            Assert.Contains("scaler-a", result);

            // Add another and verify both are present
            await _repository.AddAsync("scaler-b", CancellationToken.None);
            content = await blobClient.DownloadTextAsync();
            result = JsonConvert.DeserializeObject<HashSet<string>>(content);
            Assert.Equal(2, result.Count);
            Assert.Contains("scaler-a", result);
            Assert.Contains("scaler-b", result);
        }

        [Fact]
        public async Task GetAsync_ReadsExpectedBlob()
        {
            await DeleteTestBlobsAsync();

            // Write a blob directly
            string path = await _repository.GetBlobPathAsync(CancellationToken.None);
            BlobContainerClient blobContainerClient = await _repository.GetContainerClientAsync(CancellationToken.None);
            BlobClient blobClient = blobContainerClient.GetBlobClient(path);

            var testData = new HashSet<string> { "scaler-x", "scaler-y" };
            string content = JsonConvert.SerializeObject(testData);
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
        public async Task ClearAsync_DeletesBlob()
        {
            await DeleteTestBlobsAsync();

            // Add an entry so the blob exists
            await _repository.AddAsync("scaler-a", CancellationToken.None);
            var errors = await _repository.GetAsync(CancellationToken.None);
            Assert.Single(errors);

            // Clear
            await _repository.ClearAsync(CancellationToken.None);

            // Verify blob is gone and GetAsync returns empty
            var path = await _repository.GetBlobPathAsync(CancellationToken.None);
            BlobContainerClient blobContainerClient = await _repository.GetContainerClientAsync(CancellationToken.None);
            BlobClient blobClient = blobContainerClient.GetBlobClient(path);
            bool exists = await blobClient.ExistsAsync();
            Assert.False(exists);

            errors = await _repository.GetAsync(CancellationToken.None);
            Assert.Empty(errors);
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

            await localRepository.ClearAsync(CancellationToken.None);
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
