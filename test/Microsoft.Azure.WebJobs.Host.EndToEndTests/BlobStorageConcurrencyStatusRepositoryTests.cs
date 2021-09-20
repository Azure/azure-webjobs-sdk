// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    [Trait(TestTraits.CategoryTraitName, TestTraits.DynamicConcurrency)]
    public class BlobStorageConcurrencyStatusRepositoryTests
    {
        private const string TestHostId = "test123";
        private readonly BlobStorageConcurrencyStatusRepository _repository;
        private readonly LoggerFactory _loggerFactory;
        private readonly TestLoggerProvider _loggerProvider;
        private readonly HostConcurrencySnapshot _testSnapshot;
        private readonly Mock<IHostIdProvider> _mockHostIdProvider;

        public BlobStorageConcurrencyStatusRepositoryTests()
        {
            _testSnapshot = new HostConcurrencySnapshot
            {
                NumberOfCores = 4,
                Timestamp = DateTime.UtcNow
            };
            _testSnapshot.FunctionSnapshots = new Dictionary<string, FunctionConcurrencySnapshot>
            {
                { "function0", new FunctionConcurrencySnapshot { Concurrency = 5 } },
                { "function1", new FunctionConcurrencySnapshot { Concurrency = 10 } },
                { "function2", new FunctionConcurrencySnapshot { Concurrency = 15 } }
            };

            _loggerFactory = new LoggerFactory();
            _loggerProvider = new TestLoggerProvider();
            _loggerFactory.AddProvider(_loggerProvider);

            IConfiguration configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            _mockHostIdProvider = new Mock<IHostIdProvider>(MockBehavior.Strict);
            _mockHostIdProvider.Setup(p => p.GetHostIdAsync(CancellationToken.None)).ReturnsAsync(TestHostId);

            var mockAzureStorageProvider = new Mock<IAzureStorageProvider>(MockBehavior.Strict);
            mockAzureStorageProvider.Setup(p => p.GetWebJobsBlobContainerClient()).Returns(new BlobContainerClient(configuration.GetWebJobsConnectionString(ConnectionStringNames.Storage), HostContainerNames.Hosts));

            _repository = new BlobStorageConcurrencyStatusRepository(configuration, mockAzureStorageProvider.Object, _mockHostIdProvider.Object, _loggerFactory);
        }

        [Fact]
        public async Task GetContainerAsync_ReturnsExpectedContainer()
        {
            BlobContainerClient container = await _repository.GetContainerAsync(CancellationToken.None);
            Assert.Equal(HostContainerNames.Hosts, container.Name);
        }

        [Fact]
        public async Task GetBlobPathAsync_ReturnsExpectedPath()
        {
            string path = await _repository.GetBlobPathAsync(CancellationToken.None);

            Assert.Equal($"concurrency/{TestHostId}/concurrencyStatus.json", path);
        }

        [Fact]
        public async Task WriteAsync_WritesExpectedBlob()
        {
            await DeleteTestBlobsAsync();

            var path = await _repository.GetBlobPathAsync(CancellationToken.None);
            BlobContainerClient container = await _repository.GetContainerAsync(CancellationToken.None);
            BlobClient blob = container.GetBlobClient(path);
            bool exists = await blob.ExistsAsync();
            Assert.False(exists);

            await _repository.WriteAsync(_testSnapshot, CancellationToken.None);

            exists = await blob.ExistsAsync();
            Assert.True(exists);

            var downloadResponse = await blob.DownloadTextAsync();
            string content = downloadResponse.Trim(new char[] { '\uFEFF', '\u200B' });

            var result = JsonConvert.DeserializeObject<HostConcurrencySnapshot>(content);

            Assert.True(_testSnapshot.Equals(result));
        }

        [Fact]
        public async Task ReadAsyncAsync_ReadsExpectedBlob()
        {
            await DeleteTestBlobsAsync();

            string path = await _repository.GetBlobPathAsync(CancellationToken.None);
            BlobContainerClient container = await _repository.GetContainerAsync(CancellationToken.None);
            BlobClient blob = container.GetBlobClient(path);

            string content = JsonConvert.SerializeObject(_testSnapshot);
            await blob.UploadTextAsync(content);

            var result = await _repository.ReadAsync(CancellationToken.None);

            Assert.True(_testSnapshot.Equals(result));
        }

        [Fact]
        public async Task ReadAsyncAsync_NoSnapshot_ReturnsNull()
        {
            await DeleteTestBlobsAsync();

            var result = await _repository.ReadAsync(CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task NoStorageConnection_HandledGracefully()
        {
            IConfiguration configuration = new ConfigurationBuilder().Build();
            var mockAzureStorageProvider = new Mock<IAzureStorageProvider>(MockBehavior.Strict);
            mockAzureStorageProvider.Setup(p => p.GetWebJobsBlobContainerClient()).Throws(new Exception("Could not create BlobContainerClient in IAzureStorageProvider mock."));

            var localRepository = new BlobStorageConcurrencyStatusRepository(configuration, mockAzureStorageProvider.Object, _mockHostIdProvider.Object, _loggerFactory);

            var container = await localRepository.GetContainerAsync(CancellationToken.None);
            Assert.Null(container);

            await localRepository.WriteAsync(new HostConcurrencySnapshot(), CancellationToken.None);

            var snapshot = await localRepository.ReadAsync(CancellationToken.None);
            Assert.Null(snapshot);
        }

        private async Task DeleteTestBlobsAsync()
        {
            var containerClient = await _repository.GetContainerAsync(CancellationToken.None);
            await foreach (var blob in containerClient.GetBlobsAsync(prefix: $"concurrency/{TestHostId}"))
            {
                await containerClient.DeleteBlobAsync(blob.Name);
            }
        }
    }
}