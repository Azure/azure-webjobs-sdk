// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using static Microsoft.Azure.WebJobs.Host.BlobLeaseDistributedLockManager;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Singleton
{
    public class SingletonManagerStorageIntegrationTests : IAsyncLifetime
    {
        private const string TestArtifactContainerPrefix = "e2e-singletonmanagertests";
        private const string TestArtifactContainerName = TestArtifactContainerPrefix + "-%rnd%";

        private const string TestHostId = "testhost";
        private const string TestLockId = "testid";
        private const string TestInstanceId = "testinstance";

        private BlobLeaseDistributedLockManager _core;
        private SingletonManager _singletonManager;
        private SingletonOptions _singletonConfig;

        private Mock<IWebJobsExceptionHandler> _mockExceptionDispatcher;

        private readonly BlobContainerClient _testContainerClient;

        private TestLoggerProvider _loggerProvider;
        private TestNameResolver _nameResolver;

        internal class TestBlobLeaseDistributedLockManager : BlobLeaseDistributedLockManager
        {
            // To set up testing
            public BlobContainerClient ContainerClient;

            public TestBlobLeaseDistributedLockManager(ILoggerFactory logger, IAzureBlobStorageProvider blobStorageProvider) : base(logger, blobStorageProvider) { }

            protected override BlobContainerClient GetContainerClient(string accountName)
            {
                if (!string.IsNullOrWhiteSpace(accountName))
                {
                    throw new InvalidOperationException("This test does not support multiple accounts.");
                }

                return ContainerClient;
            }

            // Testing the base method to verify we can use multiple storage accounts
            internal BlobContainerClient BaseGetContainerClient(string accountName)
            {
                return base.GetContainerClient(accountName);
            }
        }

        // All dependencies of SingletonManager is mocked except Blob storage
        public SingletonManagerStorageIntegrationTests()
        {
            ILoggerFactory loggerFactory = new LoggerFactory();
            _loggerProvider = new TestLoggerProvider();
            loggerFactory.AddProvider(_loggerProvider);

            var testBlobLeaseDistributedLockManager = new TestBlobLeaseDistributedLockManager(loggerFactory, null);
            var blobServiceClient = TestHelpers.GetTestBlobServiceClient();

            _testContainerClient = blobServiceClient.GetBlobContainerClient(new RandomNameResolver().ResolveInString(TestArtifactContainerName));
            testBlobLeaseDistributedLockManager.ContainerClient = _testContainerClient;
            _core = testBlobLeaseDistributedLockManager;

            _mockExceptionDispatcher = new Mock<IWebJobsExceptionHandler>(MockBehavior.Strict);

            _singletonConfig = new SingletonOptions();

            // use reflection to bypass the normal validations (so tests can run fast)
            TestHelpers.SetField(_singletonConfig, "_lockAcquisitionPollingInterval", TimeSpan.FromMilliseconds(500));
            TestHelpers.SetField(_singletonConfig, "_lockPeriod", TimeSpan.FromSeconds(15));
            _singletonConfig.LockAcquisitionTimeout = TimeSpan.FromSeconds(16);

            _nameResolver = new TestNameResolver();

            _singletonManager = new SingletonManager(_core, new OptionsWrapper<SingletonOptions>(_singletonConfig), _mockExceptionDispatcher.Object, loggerFactory, new FixedHostIdProvider(TestHostId), _nameResolver);

            _singletonManager.MinimumLeaseRenewalInterval = TimeSpan.FromMilliseconds(250);
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            await _testContainerClient.DeleteIfExistsAsync();
        }

        [Fact]
        public async Task TryLockAsync_CreatesBlob_WhenItDoesNotExist()
        {
            CancellationToken cancellationToken = new CancellationToken();

            // Create the container as this is only testing that the blob is created
            await _testContainerClient.CreateIfNotExistsAsync();

            await foreach (var _ in _testContainerClient.GetBlobsAsync())
            {
                Assert.False(true, "Blob already exists. This shouldn't happen.");
            }

            SingletonAttribute attribute = new SingletonAttribute();
            RenewableLockHandle lockHandle = await _singletonManager.TryLockAsync(TestLockId, TestInstanceId, attribute, cancellationToken);
            var innerHandle = lockHandle.GetInnerHandle();

            Assert.Equal(TestLockId, innerHandle.LockId);

            // Shouldn't be able to lease this blob
            var exception = await Assert.ThrowsAnyAsync<RequestFailedException>(async () => await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(15)));
            Assert.Equal(409, exception.Status); // Conflict

            // Checks Instance Id in metadata
            var metadata = (await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetPropertiesAsync()).Value.Metadata;
            Assert.Single(metadata.Keys);
            Assert.Equal(TestInstanceId, metadata[BlobLeaseDistributedLockManager.FunctionInstanceMetadataKey]);
            Assert.NotNull(lockHandle);
            Assert.NotNull(lockHandle.LeaseRenewalTimer);

            // Verify the leaseId in the innerHandle is the actual active LeaseId. Otherwise, ChangeAsync will fail.
            await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetBlobLeaseClient(innerHandle.LeaseId).ChangeAsync(Guid.NewGuid().ToString());
        }

        [Fact]
        public async Task TryLockAsync_CreatesBlobContainer_WhenItDoesNotExist()
        {
            CancellationToken cancellationToken = new CancellationToken();

            Assert.False(_testContainerClient.Exists());

            SingletonAttribute attribute = new SingletonAttribute();
            RenewableLockHandle lockHandle = await _singletonManager.TryLockAsync(TestLockId, TestInstanceId, attribute, cancellationToken);
            var innerHandle = lockHandle.GetInnerHandle();

            Assert.Equal(TestLockId, innerHandle.LockId);

            // Shouldn't be able to lease this blob
            var exception = await Assert.ThrowsAnyAsync<RequestFailedException>(async () => await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(15)));
            Assert.Equal(409, exception.Status); // Conflict

            // Checks Instance Id in metadata
            var metadata = (await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetPropertiesAsync()).Value.Metadata;
            Assert.Single(metadata.Keys);
            Assert.Equal(TestInstanceId, metadata[BlobLeaseDistributedLockManager.FunctionInstanceMetadataKey]);
            Assert.NotNull(lockHandle);
            Assert.NotNull(lockHandle.LeaseRenewalTimer);

            // Verify the leaseId in the innerHandle is the actual active LeaseId. Otherwise, ChangeAsync will fail.
            await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetBlobLeaseClient(innerHandle.LeaseId).ChangeAsync(Guid.NewGuid().ToString());
        }

        [Fact]
        public async Task TryLockAsync_CreatesBlobLease_WithAutoRenewal()
        {
            CancellationToken cancellationToken = new CancellationToken();

            SingletonAttribute attribute = new SingletonAttribute();
            var lockHandle = await _singletonManager.TryLockAsync(TestLockId, TestInstanceId, attribute, cancellationToken);
            var innerHandle = lockHandle.GetInnerHandle();

            Assert.Equal(TestLockId, innerHandle.LockId);

            // Shouldn't be able to lease this blob yet
            var exception = await Assert.ThrowsAnyAsync<RequestFailedException>(async () => await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(15)));
            Assert.Equal(409, exception.Status); // Conflict

            // Wait for enough time that we expect some lease renewals to occur
            var duration = _singletonConfig.LockPeriod.TotalMilliseconds + TimeSpan.FromSeconds(1).TotalMilliseconds;
            await Task.Delay((int)duration);

            // Still shouldn't be able to lease this blob yet
            exception = await Assert.ThrowsAnyAsync<RequestFailedException>(async () => await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(15)));
            Assert.Equal(409, exception.Status); // Conflict

            // Now release the lock and verify no more renewals
            await _singletonManager.ReleaseLockAsync(lockHandle, cancellationToken);

            // Should be able to lease this blob now in the test context
            await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(15));

            // verify the logger
            TestLogger logger = _loggerProvider.CreatedLoggers.Single() as TestLogger;
            Assert.Equal(LogCategories.Singleton, logger.Category);
            var messages = logger.GetLogMessages();
            Assert.Equal(2, messages.Count);
            Assert.NotNull(messages.Single(m => m.Level == Microsoft.Extensions.Logging.LogLevel.Debug && m.FormattedMessage == "Singleton lock acquired (testid)"));
            Assert.NotNull(messages.Single(m => m.Level == Microsoft.Extensions.Logging.LogLevel.Debug && m.FormattedMessage == "Singleton lock released (testid)"));

            Assert.NotNull(lockHandle);
            Assert.NotNull(lockHandle.LeaseRenewalTimer);
        }

        [Fact]
        public async Task TryLockAsync_WithContention_PollsForLease()
        {
            CancellationToken cancellationToken = new CancellationToken();

            // Setup test so that the blob exists
            await _testContainerClient.CreateIfNotExistsAsync();
            if (!_testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).Exists())
            {
                await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).UploadTextAsync("", overwrite: true);
            }

            // Lease this blob in the test context to test polling behavior of the SingletonManager
            await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(15));

            // SingletonManager will be trying to poll for the lock and eventually get it after 15 seconds
            SingletonAttribute attribute = new SingletonAttribute();
            var lockHandle = await _singletonManager.TryLockAsync(TestLockId, TestInstanceId, attribute, cancellationToken);
            var innerHandle = lockHandle.GetInnerHandle();
            Assert.Equal(TestLockId, innerHandle.LockId);

            // Shouldn't be able to lease this blob since the SingletonManager has the lease
            var exception = await Assert.ThrowsAnyAsync<RequestFailedException>(async () => await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(15)));
            Assert.Equal(409, exception.Status); // Conflict

            // Checks Instance Id in metadata
            var metadata = (await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetPropertiesAsync()).Value.Metadata;
            Assert.Single(metadata.Keys);
            Assert.Equal(TestInstanceId, metadata[BlobLeaseDistributedLockManager.FunctionInstanceMetadataKey]);
            Assert.NotNull(lockHandle);
            Assert.NotNull(lockHandle.LeaseRenewalTimer);
        }

        [Fact]
        public async Task TryLockAsync_WithContention_NoRetry_DoesNotPollForLease()
        {
            var task = Task.Run(async () =>
            {
                CancellationToken cancellationToken = new CancellationToken();

                // Setup test so that the blob exists
                await _testContainerClient.CreateIfNotExistsAsync();
                if (!_testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).Exists())
                {
                    await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).UploadTextAsync("", overwrite: true);
                }

                // Lease this blob in the test context
                var lease = await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(15));

                // SingletonManager will not poll for the lease; test should end quickly
                SingletonAttribute attribute = new SingletonAttribute();
                var lockHandle = await _singletonManager.TryLockAsync(TestLockId, TestInstanceId, attribute, cancellationToken, retry: false);

                Assert.Null(lockHandle);
            });

            if (task.Wait(1500))
            {
                await task;
            }
            else
            {
                throw new TimeoutException("Test ran too long. The SingletonManager is likely retrying to get lease");
            }
        }

        [Fact]
        public async Task LockAsync_WithContention_AcquisitionTimeoutExpires_Throws()
        {
            CancellationToken cancellationToken = new CancellationToken();

            // Setup test so that the blob exists
            await _testContainerClient.CreateIfNotExistsAsync();
            if (!_testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).Exists())
            {
                await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).UploadTextAsync("", overwrite: true);
            }

            // Lease this blob in the test context
            var lease = await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(15));

            // Failure to acquire lock is expected with a custom error message
            SingletonAttribute attribute = new SingletonAttribute();
            attribute.LockAcquisitionTimeout = 1;
            TimeoutException exception = await Assert.ThrowsAsync<TimeoutException>(async () => await _singletonManager.LockAsync(TestLockId, TestInstanceId, attribute, cancellationToken));
            Assert.Equal("Unable to acquire singleton lock blob lease for blob 'testid' (timeout of 0:00:01 exceeded).", exception.Message);
        }

        [Fact]
        public async Task ReleaseLockAsync_StopsRenewalTimerAndReleasesLease()
        {
            CancellationToken cancellationToken = new CancellationToken();

            SingletonAttribute attribute = new SingletonAttribute();
            var lockHandle = await _singletonManager.TryLockAsync(TestLockId, TestInstanceId, attribute, cancellationToken);
            var innerHandle = lockHandle.GetInnerHandle();

            await _singletonManager.ReleaseLockAsync(lockHandle, cancellationToken);

            // Timer should've been stopped
            await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => await lockHandle.LeaseRenewalTimer.StopAsync(cancellationToken));

            // Verify lease has been released by SingletonManager
            var lease = await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(15));
        }

        [Fact]
        public async Task ReleaseLockAsync_FunctionCancellationTokenCancelled_ThrowsCancellationException()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            SingletonAttribute attribute = new SingletonAttribute();
            var lockHandle = await _singletonManager.TryLockAsync(TestLockId, TestInstanceId, attribute, cancellationToken);

            // Cancel token source
            cancellationTokenSource.Cancel();

            // ReleaseLockAsync should throw an exception
            await Assert.ThrowsAnyAsync<TaskCanceledException>(async () => await _singletonManager.ReleaseLockAsync(lockHandle, cancellationToken));

            // Timer should've been stopped
            await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => await lockHandle.LeaseRenewalTimer.StopAsync(cancellationToken));
        }

        [Fact]
        public async Task ReleaseLockAsync_CancellationTokenNone_ReleasesLease_DoesNotThrow()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            SingletonAttribute attribute = new SingletonAttribute();
            var lockHandle = await _singletonManager.TryLockAsync(TestLockId, TestInstanceId, attribute, cancellationToken);

            await _singletonManager.ReleaseLockAsync(lockHandle, CancellationToken.None);

            // Timer should've been stopped
            await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => await lockHandle.LeaseRenewalTimer.StopAsync(cancellationToken));

            // Verify lease has been released by SingletonManager
            var lease = await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(15));
        }

        [Fact]
        public async Task GetLockOwnerAsync_LeaseLocked_ReturnsOwner()
        {
            CancellationToken cancellationToken = new CancellationToken();

            Assert.False(_testContainerClient.Exists());

            // No owner yet and no blob/container
            SingletonAttribute attribute = new SingletonAttribute();
            string lockOwner = await _singletonManager.GetLockOwnerAsync(attribute, TestLockId, cancellationToken);
            Assert.Equal(null, lockOwner);

            await _testContainerClient.CreateIfNotExistsAsync();
            if (!_testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).Exists())
            {
                await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).UploadTextAsync("", overwrite: true);
            }

            var properties = (await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetPropertiesAsync()).Value;
            Assert.Equal(LeaseState.Available, properties.LeaseState);
            Assert.Equal(LeaseStatus.Unlocked, properties.LeaseStatus);

            // No owner but blob/container exists
            lockOwner = await _singletonManager.GetLockOwnerAsync(attribute, TestLockId, cancellationToken);
            Assert.Equal(null, lockOwner);

            // Get a lease
            var lockHandle = await _singletonManager.TryLockAsync(TestLockId, TestInstanceId, attribute, cancellationToken);

            // There should be an owner
            lockOwner = await _singletonManager.GetLockOwnerAsync(attribute, TestLockId, cancellationToken);
            Assert.Equal(TestInstanceId, lockOwner);

            // Shouldn't be able to lease this blob
            await Assert.ThrowsAnyAsync<Exception>(async () => await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(15)));
        }

        private class TestNameResolver : INameResolver
        {
            public TestNameResolver()
            {
                Names = new Dictionary<string, string>();
            }

            public Dictionary<string, string> Names { get; private set; }

            public string Resolve(string name)
            {
                if (Names.TryGetValue(name, out string value))
                {
                    return value;
                }
                throw new NotSupportedException(string.Format("Cannot resolve name: '{0}'", name));
            }
        }
    }
}
