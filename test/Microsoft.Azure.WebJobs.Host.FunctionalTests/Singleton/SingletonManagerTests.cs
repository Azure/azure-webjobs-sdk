// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using SingletonLockHandle = Microsoft.Azure.WebJobs.Host.BlobLeaseDistributedLockManager.SingletonLockHandle;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Singleton
{
    internal static class Ext // $$$ move to better place
    {
        // Wrapper to get the internal class. 
        public static async Task<SingletonLockHandle> TryLockInternalAsync(this SingletonManager manager, string lockId, string functionInstanceId, SingletonAttribute attribute, CancellationToken cancellationToken, bool retry = true)
        {
            var handle = await manager.TryLockAsync(lockId, functionInstanceId, attribute, cancellationToken, retry);
            return handle.GetInnerHandle();
        }

        public static SingletonLockHandle GetInnerHandle(this RenewableLockHandle handle)
        {
            if (handle == null)
            {
                return null;
            }
            return (SingletonLockHandle)handle.InnerLock;
        }
    }

    public class SingletonManagerTests
    {
        private const string TestHostId = "testhost";
        private const string TestLockId = "testid";
        private const string TestInstanceId = "testinstance";
        private const string TestLeaseId = "testleaseid";
        private const string Secondary = "SecondaryStorage";

        private readonly IConfiguration _configuration = new ConfigurationBuilder().Build();

        private BlobLeaseDistributedLockManager _core;
        private SingletonManager _singletonManager;
        private SingletonOptions _singletonConfig;

        private MockAzureBlobStorageAccount _account = new MockAzureBlobStorageAccount(ConnectionStringNames.Storage);

        private Mock<IWebJobsExceptionHandler> _mockExceptionDispatcher;
        private TestLoggerProvider _loggerProvider;
        private TestNameResolver _nameResolver;

        private class FakeLeaseProvider : BlobLeaseDistributedLockManager
        {
            private readonly MockAzureBlobStorageAccount _account;

            public FakeLeaseProvider(ILoggerFactory logger,
                IAzureBlobStorageProvider azureBlobStorageProvider,
                MockAzureBlobStorageAccount account)
                : base(logger, azureBlobStorageProvider)
            {
                _account = account;
            }

            protected override BlobContainerClient GetContainerClient(string accountName)
            {
                return _account.BlobContainerClient.Object;
            }

            protected override BlobLeaseClient GetBlobLeaseClient(BlobClient blobClient, string proposedLeaseId)
            {
                return _account.BlobLeaseClient.Object;
            }

            protected override BlobContainerClient GetParentBlobContainerClient(BlobClient blobClient)
            {
                return _account.BlobContainerClient.Object;
            }

            // Testing the base method to verify we can use multiple storage accounts
            internal BlobContainerClient BaseGetContainerClient(string accountName)
            {
                return base.GetContainerClient(accountName);
            }
        }

        /// <summary>
        /// This is the mocking layer for Azure Blob storage objects. This private class extensively relies on reflection to construct and edit
        /// classes in <see cref="Azure.Storage.Blobs.Models"/> since they have internal constructors and no setters.
        /// Relationship is BlobContainerClient <-- has --> BlobClient <-- has --> BlobLeaseClient.
        /// The tests in <see cref="SingletonManagerTests"/> use this class as a utility.
        /// </summary>
        private class MockAzureBlobStorageAccount
        {
            public MockAzureBlobStorageAccount(string accountName)
            {
                AccountName = accountName;

                BlobClient = new Mock<BlobClient>(MockBehavior.Strict);
                BlobContainerClient = new Mock<BlobContainerClient>(MockBehavior.Strict);
                BlobContainerClient.Setup(b => b.GetBlobClient(It.IsAny<string>())).Returns(BlobClient.Object);

                BlobLeaseClient = new Mock<BlobLeaseClient>(MockBehavior.Strict);
            }

            public string AccountName { get; private set; }

            public Mock<BlobContainerClient> BlobContainerClient { get; private set; }

            public Mock<BlobClient> BlobClient { get; private set; }

            public Mock<BlobLeaseClient> BlobLeaseClient { get; private set; }

            public IDictionary<string, string> LastSetMetadata { get; set; }

            public Response<ReleasedObjectInfo> CreateReleasedObjectInfoResponse()
            {
                var blobInfo = CreateInternalObjectHelper<BlobInfo>();
                ConstructorInfo c = typeof(ReleasedObjectInfo).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[1] { typeof(BlobInfo) }, null);
                var releasedObjectInfo = (ReleasedObjectInfo)c.Invoke(BindingFlags.NonPublic, null, new object[1] { blobInfo }, null);
                return Response.FromValue(releasedObjectInfo, default);
            }

            public Response<T> CreateInternalAzureBlobResponse<T>(Dictionary<string, object> fields = null)
            {
                T obj = CreateInternalObjectHelper<T>();
                if (fields != null)
                {
                    var allFields = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
                    foreach (var field in fields)
                    {
                        var f = allFields.Where(f => f.Name.Contains(field.Key)).SingleOrDefault();
                        f.SetValue(obj, field.Value);
                    }
                }

                return Response.FromValue(obj, default);
            }

            private static T CreateInternalObjectHelper<T>()
            {
                ConstructorInfo c = typeof(T).GetConstructor(new Type[0]) ?? typeof(T).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[0], null);
                return (T)c.Invoke(BindingFlags.NonPublic, null, null, null);
            }

            public static Dictionary<string, object> AddObjectField(Dictionary<string, object> fields, string fieldName, object fieldValue)
            {
                fields = fields ?? new Dictionary<string, object>();
                if (!fields.ContainsKey(fieldName))
                {
                    fields[fieldName] = fieldValue;
                }

                return fields;
            }
        }

        public SingletonManagerTests()
        {
            ILoggerFactory loggerFactory = new LoggerFactory();
            _loggerProvider = new TestLoggerProvider();
            loggerFactory.AddProvider(_loggerProvider);

            var logger = loggerFactory?.CreateLogger(LogCategories.Singleton);

            var leaseProvider = new FakeLeaseProvider(loggerFactory, null, _account);

            _mockExceptionDispatcher = new Mock<IWebJobsExceptionHandler>(MockBehavior.Strict);

            _singletonConfig = new SingletonOptions();

            // use reflection to bypass the normal validations (so tests can run fast)
            TestHelpers.SetField(_singletonConfig, "_lockAcquisitionPollingInterval", TimeSpan.FromMilliseconds(25));
            TestHelpers.SetField(_singletonConfig, "_lockPeriod", TimeSpan.FromMilliseconds(500));
            _singletonConfig.LockAcquisitionTimeout = TimeSpan.FromMilliseconds(200);

            _nameResolver = new TestNameResolver();
            _core = leaseProvider;
            _singletonManager = new SingletonManager(_core, new OptionsWrapper<SingletonOptions>(_singletonConfig), _mockExceptionDispatcher.Object, loggerFactory, new FixedHostIdProvider(TestHostId), _nameResolver);

            _singletonManager.MinimumLeaseRenewalInterval = TimeSpan.FromMilliseconds(250);
        }

        [Fact]
        public void GetLockPath()
        {
            var path = _core.GetLockPath(ConnectionStringNames.Storage);
            var expectedPath = string.Format("{0}/{1}", HostDirectoryNames.SingletonLocks, ConnectionStringNames.Storage);
            Assert.Equal(expectedPath, path);
        }

        // This test should not make any calls to storage. We are just creating clients.
        [Fact]
        public void GetContainerClient_SupportsMultipleAccounts()
        {
            var blobStorageProvider = TestHelpers.GetTestAzureBlobStorageProvider();
            var blobLockManager = new FakeLeaseProvider(new LoggerFactory(), blobStorageProvider, null);

            // Testing primary account
            blobStorageProvider.TryCreateBlobServiceClientFromConnection(ConnectionStringNames.Storage, out BlobServiceClient blobServiceClient);
            var accountContainer = blobLockManager.BaseGetContainerClient(ConnectionStringNames.Storage);
            Assert.Equal(blobServiceClient.AccountName, accountContainer.AccountName);

            // Testing primary account by providing null accountName argument
            blobStorageProvider.TryCreateBlobServiceClientFromConnection(ConnectionStringNames.Storage, out blobServiceClient);
            accountContainer = blobLockManager.BaseGetContainerClient(null);
            Assert.Equal(blobServiceClient.AccountName, accountContainer.AccountName);

            // Testing secondary account
            blobStorageProvider.TryCreateBlobServiceClientFromConnection(Secondary, out blobServiceClient);
            accountContainer = blobLockManager.BaseGetContainerClient(Secondary);
            Assert.Equal(blobServiceClient.AccountName, accountContainer.AccountName);
        }

        [Fact]
        public async Task TryLockAsync_CreatesBlob_WhenItDoesNotExist()
        {
            CancellationToken cancellationToken = new CancellationToken();
            RequestFailedException storageException = new RequestFailedException(404, "Not Found Test Error");

            var mockAccount = _account;

            int count = 0;

            MockAcquireLeaseAsync(mockAccount, null, () =>
            {
                if (count++ == 0)
                {
                    throw storageException;
                }

                var fields = new Dictionary<string, object>()
                {
                    { "LeaseId", TestLeaseId },
                };
                return mockAccount.CreateInternalAzureBlobResponse<BlobLease>(fields);
            });

            // First call to create the blob should fail because container does not exist
            int uploadAsyncCallCount = 0;
            mockAccount.BlobContainerClient.Setup(p => p.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(mockAccount.CreateInternalAzureBlobResponse<BlobContainerInfo>()));
            mockAccount.BlobClient.Setup(p => p.UploadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (uploadAsyncCallCount++ == 0)
                    {
                        throw storageException;
                    }
                    return Task.FromResult(mockAccount.CreateInternalAzureBlobResponse<BlobContentInfo>());
                });
            mockAccount.BlobClient.Setup(p => p.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(mockAccount.CreateInternalAzureBlobResponse<BlobProperties>()));
            mockAccount.BlobClient.Setup(p => p.SetMetadataAsync(It.IsAny<IDictionary<string, string>>(), It.Is<BlobRequestConditions>(q => q.LeaseId == TestLeaseId), It.IsAny<CancellationToken>()))
                .Callback<IDictionary<string, string>, BlobRequestConditions, CancellationToken>((metadata, conditions, cancellationToken) => mockAccount.LastSetMetadata = metadata)
                .Returns(Task.FromResult(mockAccount.CreateInternalAzureBlobResponse<BlobInfo>()));

            SingletonAttribute attribute = new SingletonAttribute();
            RenewableLockHandle lockHandle = await _singletonManager.TryLockAsync(TestLockId, TestInstanceId, attribute, cancellationToken);
            var innerHandle = lockHandle.GetInnerHandle();

            Assert.Equal(mockAccount.BlobLeaseClient.Object, innerHandle.BlobLeaseClient);
            Assert.Equal(TestLeaseId, innerHandle.LeaseId);
            Assert.Single(mockAccount.LastSetMetadata.Keys);
            Assert.Equal(mockAccount.LastSetMetadata[BlobLeaseDistributedLockManager.FunctionInstanceMetadataKey], TestInstanceId);

            mockAccount.BlobContainerClient.VerifyAll();
            mockAccount.BlobClient.VerifyAll();
            mockAccount.BlobLeaseClient.VerifyAll();
        }

        [Fact]
        public async Task TryLockAsync_CreatesBlobLease_WithAutoRenewal()
        {
            CancellationToken cancellationToken = new CancellationToken();

            var mockAccount = _account;
            MockAcquireLeaseAsync(mockAccount, null, () =>
            {
                var fields = new Dictionary<string, object>()
                {
                    { "LeaseId", TestLeaseId },
                };
                return mockAccount.CreateInternalAzureBlobResponse<BlobLease>(fields);
            });

            mockAccount.BlobClient.Setup(p => p.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(mockAccount.CreateInternalAzureBlobResponse<BlobProperties>()));
            mockAccount.BlobClient.Setup(p => p.SetMetadataAsync(It.IsAny<IDictionary<string, string>>(), It.Is<BlobRequestConditions>(q => q.LeaseId == TestLeaseId), It.IsAny<CancellationToken>()))
                .Callback<IDictionary<string, string>, BlobRequestConditions, CancellationToken>((metadata, conditions, cancellationToken) => mockAccount.LastSetMetadata = metadata)
                .Returns(Task.FromResult(mockAccount.CreateInternalAzureBlobResponse<BlobInfo>()));
            mockAccount.BlobLeaseClient.Setup(p => p.ReleaseAsync(It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(mockAccount.CreateReleasedObjectInfoResponse()));

            int renewCount = 0;

            mockAccount.BlobLeaseClient.Setup(p => p.RenewAsync(It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>()))
                .Callback<RequestConditions, CancellationToken>((requestCondition, cancellationToken) =>
                {
                    renewCount++;
                })
                .Returns(Task.FromResult(mockAccount.CreateInternalAzureBlobResponse<BlobLease>()));

            SingletonAttribute attribute = new SingletonAttribute();
            var lockHandle = await _singletonManager.TryLockAsync(TestLockId, TestInstanceId, attribute, cancellationToken);
            var innerHandle = lockHandle.GetInnerHandle();

            Assert.Equal(mockAccount.BlobLeaseClient.Object, innerHandle.BlobLeaseClient);
            Assert.Equal(TestLeaseId, innerHandle.LeaseId);
            Assert.Single(mockAccount.LastSetMetadata.Keys);
            Assert.Equal(mockAccount.LastSetMetadata[BlobLeaseDistributedLockManager.FunctionInstanceMetadataKey], TestInstanceId);

            // wait for enough time that we expect some lease renewals to occur
            int duration = 2000;
            int expectedRenewalCount = (int)(duration / (_singletonConfig.LockPeriod.TotalMilliseconds / 2)) - 1;
            await Task.Delay(duration);

            Assert.Equal(expectedRenewalCount, renewCount);

            // now release the lock and verify no more renewals
            await _singletonManager.ReleaseLockAsync(lockHandle, cancellationToken);

            // verify the logger
            TestLogger logger = _loggerProvider.CreatedLoggers.Single() as TestLogger;
            Assert.Equal(LogCategories.Singleton, logger.Category);
            var messages = logger.GetLogMessages();
            Assert.Equal(2, messages.Count);
            Assert.NotNull(messages.Single(m => m.Level == Microsoft.Extensions.Logging.LogLevel.Debug && m.FormattedMessage == "Singleton lock acquired (testid)"));
            Assert.NotNull(messages.Single(m => m.Level == Microsoft.Extensions.Logging.LogLevel.Debug && m.FormattedMessage == "Singleton lock released (testid)"));

            renewCount = 0;
            await Task.Delay(1000);

            Assert.Equal(0, renewCount);

            mockAccount.BlobClient.VerifyAll();
            mockAccount.BlobLeaseClient.VerifyAll();
        }

        [Fact]
        public async Task TryLockAsync_WithContention_PollsForLease()
        {
            CancellationToken cancellationToken = new CancellationToken();

            var mockAccount = _account;
            mockAccount.BlobClient.Setup(p => p.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(mockAccount.CreateInternalAzureBlobResponse<BlobProperties>()));
            mockAccount.BlobClient.Setup(p => p.SetMetadataAsync(It.IsAny<IDictionary<string, string>>(), It.Is<BlobRequestConditions>(q => q.LeaseId == TestLeaseId), It.IsAny<CancellationToken>()))
                .Callback<IDictionary<string, string>, BlobRequestConditions, CancellationToken>((metadata, conditions, cancellationToken) => mockAccount.LastSetMetadata = metadata)
                .Returns(Task.FromResult(mockAccount.CreateInternalAzureBlobResponse<BlobInfo>()));

            int numRetries = 3;
            int count = 0;

            MockAcquireLeaseAsync(mockAccount, null, () =>
            {
                count++;
                if (count > numRetries)
                {
                    var fields = new Dictionary<string, object>()
                    {
                        { "LeaseId", TestLeaseId },
                    };
                    return mockAccount.CreateInternalAzureBlobResponse<BlobLease>(fields);
                }

                throw new RequestFailedException(409, "Failed to get lease in test");
            });

            SingletonAttribute attribute = new SingletonAttribute();
            var lockHandle = await _singletonManager.TryLockAsync(TestLockId, TestInstanceId, attribute, cancellationToken);
            var innerHandle = lockHandle.GetInnerHandle();

            Assert.NotNull(lockHandle);
            Assert.Equal(TestLeaseId, innerHandle.LeaseId);
            Assert.Equal(numRetries, count - 1);
            Assert.NotNull(lockHandle.LeaseRenewalTimer);

            mockAccount.BlobClient.VerifyAll();
            mockAccount.BlobLeaseClient.VerifyAll();
        }

        [Fact]
        public async Task TryLockAsync_WithContention_NoRetry_DoesNotPollForLease()
        {
            CancellationToken cancellationToken = new CancellationToken();

            int count = 0;
            var mockAccount = _account;
            MockAcquireLeaseAsync(mockAccount, null, () =>
            {
                count++;
                throw new RequestFailedException(409, "Failed to get lease in test");
            });

            mockAccount.BlobClient.Setup(p => p.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(mockAccount.CreateInternalAzureBlobResponse<BlobProperties>()));

            SingletonAttribute attribute = new SingletonAttribute();
            SingletonLockHandle lockHandle = await _singletonManager.TryLockInternalAsync(TestLockId, TestInstanceId, attribute, cancellationToken, retry: false);

            Assert.Null(lockHandle);
            Assert.Equal(1, count);

            mockAccount.BlobClient.VerifyAll();
            mockAccount.BlobLeaseClient.VerifyAll();
        }

        // Helper to setup mock since the signatures are very complex
        private void MockAcquireLeaseAsync(MockAzureBlobStorageAccount mockAccount, Action fpAction, Func<Response<BlobLease>> returns)
        {
            mockAccount.BlobLeaseClient.Setup(p => p.AcquireAsync(_singletonConfig.LockPeriod, It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>()))
                .Callback<TimeSpan, RequestConditions, CancellationToken>((timeSpan, requestConditions, cancellationToken) =>
                {
                    fpAction?.Invoke();
                }).Returns(() =>
                {
                    var retResult = returns();
                    return Task.FromResult(retResult);
                });
        }

        [Fact]
        public async Task LockAsync_WithContention_AcquisitionTimeoutExpires_Throws()
        {
            CancellationToken cancellationToken = new CancellationToken();

            int count = 0;
            var mockAccount = _account;
            MockAcquireLeaseAsync(mockAccount, () =>
            {
                ++count;
            }, () => throw new RequestFailedException(409, "Failed to get lease in test"));

            mockAccount.BlobClient.Setup(p => p.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(mockAccount.CreateInternalAzureBlobResponse<BlobProperties>()));

            SingletonAttribute attribute = new SingletonAttribute();
            TimeoutException exception = await Assert.ThrowsAsync<TimeoutException>(async () => await _singletonManager.LockAsync(TestLockId, TestInstanceId, attribute, cancellationToken));

            int expectedRetryCount = (int)(_singletonConfig.LockAcquisitionTimeout.TotalMilliseconds / _singletonConfig.LockAcquisitionPollingInterval.TotalMilliseconds);
            Assert.Equal(expectedRetryCount, count - 1);
            Assert.Equal("Unable to acquire singleton lock blob lease for blob 'testid' (timeout of 0:00:00.2 exceeded).", exception.Message);

            mockAccount.BlobClient.VerifyAll();
            mockAccount.BlobLeaseClient.VerifyAll();
        }

        [Fact]
        public async Task ReleaseLockAsync_StopsRenewalTimerAndReleasesLease()
        {
            CancellationToken cancellationToken = new CancellationToken();
            var mockAccount = _account;

            Mock<ITaskSeriesTimer> mockRenewalTimer = new Mock<ITaskSeriesTimer>(MockBehavior.Strict);
            mockRenewalTimer.Setup(p => p.StopAsync(cancellationToken)).Returns(Task.FromResult(true));

            mockAccount.BlobLeaseClient.Setup(p => p.ReleaseAsync(It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(mockAccount.CreateReleasedObjectInfoResponse()));

            var handle = new RenewableLockHandle(
                new SingletonLockHandle
                {
                    BlobLeaseClient = mockAccount.BlobLeaseClient.Object,
                    LeaseId = TestLeaseId
                },
                mockRenewalTimer.Object
            );

            await _singletonManager.ReleaseLockAsync(handle, cancellationToken);

            mockRenewalTimer.VerifyAll();
        }

        [Fact]
        public async Task GetLockOwnerAsync_LeaseLocked_ReturnsOwner()
        {
            var mockAccount = _account;

            var fields = new Dictionary<string, object>()
            {
                { "LeaseState", LeaseState.Leased },
            };
            mockAccount.BlobClient.Setup(p => p.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(mockAccount.CreateInternalAzureBlobResponse<BlobProperties>(fields)));


            SingletonAttribute attribute = new SingletonAttribute();
            string lockOwner = await _singletonManager.GetLockOwnerAsync(attribute, TestLockId, CancellationToken.None);
            Assert.Equal(null, lockOwner);

            IDictionary<string, string> metadata = new Dictionary<string, string>()
            {
                { BlobLeaseDistributedLockManager.FunctionInstanceMetadataKey, TestLockId }
            };
            fields = new Dictionary<string, object>()
            {
                { "LeaseState", LeaseState.Leased },
                { "Metadata", metadata }
            };
            mockAccount.BlobClient.Setup(p => p.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(mockAccount.CreateInternalAzureBlobResponse<BlobProperties>(fields)));

            lockOwner = await _singletonManager.GetLockOwnerAsync(attribute, TestLockId, CancellationToken.None);
            Assert.Equal(TestLockId, lockOwner);

            mockAccount.BlobClient.VerifyAll();
            mockAccount.BlobLeaseClient.VerifyAll();
        }

        [Fact]
        public async Task GetLockOwnerAsync_LeaseAvailable_ReturnsNull()
        {
            var mockAccount = _account;

            var fields = new Dictionary<string, object>()
            {
                { "LeaseState", LeaseState.Available },
                { "LeaseStatus", LeaseStatus.Unlocked },
            };
            mockAccount.BlobClient.Setup(p => p.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(mockAccount.CreateInternalAzureBlobResponse<BlobProperties>(fields)));

            SingletonAttribute attribute = new SingletonAttribute();
            string lockOwner = await _singletonManager.GetLockOwnerAsync(attribute, TestLockId, CancellationToken.None);
            Assert.Equal(null, lockOwner);

            mockAccount.BlobClient.VerifyAll();
            mockAccount.BlobLeaseClient.VerifyAll();
        }

        [Theory]
        [InlineData(SingletonScope.Function, null, "TestHostId/Microsoft.Azure.WebJobs.Host.UnitTests.Singleton.SingletonManagerTests.TestJob")]
        [InlineData(SingletonScope.Function, "", "TestHostId/Microsoft.Azure.WebJobs.Host.UnitTests.Singleton.SingletonManagerTests.TestJob")]
        [InlineData(SingletonScope.Function, "testscope", "TestHostId/Microsoft.Azure.WebJobs.Host.UnitTests.Singleton.SingletonManagerTests.TestJob.testscope")]
        [InlineData(SingletonScope.Host, "testscope", "TestHostId/testscope")]
        public void FormatLockId_ReturnsExpectedValue(SingletonScope scope, string scopeId, string expectedLockId)
        {
            MethodInfo methodInfo = this.GetType().GetMethod("TestJob", BindingFlags.Static | BindingFlags.NonPublic);
            var descriptor = FunctionIndexer.FromMethod(methodInfo, _configuration);
            string actualLockId = SingletonManager.FormatLockId(descriptor, scope, "TestHostId", scopeId);
            Assert.Equal(expectedLockId, actualLockId);
        }

        [Fact]
        public void HostId_InvokesHostIdProvider_AndCachesResult()
        {
            Mock<IHostIdProvider> mockHostIdProvider = new Mock<IHostIdProvider>(MockBehavior.Strict);
            mockHostIdProvider.Setup(p => p.GetHostIdAsync(CancellationToken.None)).ReturnsAsync(TestHostId);
            SingletonManager singletonManager = new SingletonManager(null, new OptionsWrapper<SingletonOptions>(null), null, null, mockHostIdProvider.Object);

            Assert.Equal(TestHostId, singletonManager.HostId);
            Assert.Equal(TestHostId, singletonManager.HostId);
            Assert.Equal(TestHostId, singletonManager.HostId);

            mockHostIdProvider.Verify(p => p.GetHostIdAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void GetBoundScopeId_Success_ReturnsExceptedResult()
        {
            Dictionary<string, object> bindingData = new Dictionary<string, object>();
            bindingData.Add("Region", "testregion");
            bindingData.Add("Zone", 1);

            string result = _singletonManager.GetBoundScopeId(@"{Region}\{Zone}", bindingData);

            Assert.Equal(@"testregion\1", result);
        }

        [Fact]
        public void GetBoundScopeId_BindingError_Throws()
        {
            // Missing binding data for "Zone"
            Dictionary<string, object> bindingData = new Dictionary<string, object>();
            bindingData.Add("Region", "testregion");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _singletonManager.GetBoundScopeId(@"{Region}\{Zone}", bindingData));

            Assert.Equal("No value for named parameter 'Zone'.", exception.Message);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("scope", "scope")]
        public void GetBoundScopeId_NullBindingDataScenarios_Succeeds(string scope, string expectedResult)
        {
            string result = _singletonManager.GetBoundScopeId(scope, null);
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("scope", "scope")]
        [InlineData("scope{P1}", "scopeTest1")]
        [InlineData("scope:{P1}-{P2}", "scope:Test1-Test2")]
        [InlineData("%var1%", "Value1")]
        [InlineData("{P1}%var2%{P2}%var1%", "Test1Value2Test2Value1")]
        public void GetBoundScopeId_BindingDataScenarios_Succeeds(string scope, string expectedResult)
        {
            Dictionary<string, object> bindingData = new Dictionary<string, object>();
            bindingData.Add("P1", "Test1");
            bindingData.Add("P2", "Test2");

            _nameResolver.Names.Add("var1", "Value1");
            _nameResolver.Names.Add("var2", "Value2");

            string result = _singletonManager.GetBoundScopeId(scope, bindingData);
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void GetFunctionSingletonOrNull_ThrowsOnMultiple()
        {
            MethodInfo method = this.GetType().GetMethod("TestJob_MultipleFunctionSingletons", BindingFlags.Static | BindingFlags.NonPublic);

            NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            {
                SingletonManager.GetFunctionSingletonOrNull(new FunctionDescriptor()
                {
                    SingletonAttributes = method.GetCustomAttributes<SingletonAttribute>()
                }, isTriggered: true);
            });
            Assert.Equal("Only one SingletonAttribute using mode 'Function' is allowed.", exception.Message);
        }

        [Fact]
        public void GetFunctionSingletonOrNull_ListenerSingletonOnNonTriggeredFunction_Throws()
        {
            MethodInfo method = this.GetType().GetMethod("TestJob_ListenerSingleton", BindingFlags.Static | BindingFlags.NonPublic);

            NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            {
                SingletonManager.GetFunctionSingletonOrNull(new FunctionDescriptor()
                {
                    SingletonAttributes = method.GetCustomAttributes<SingletonAttribute>()
                }, isTriggered: false);
            });
            Assert.Equal("SingletonAttribute using mode 'Listener' cannot be applied to non-triggered functions.", exception.Message);
        }

        [Fact]
        public void GetListenerSingletonOrNull_ThrowsOnMultiple()
        {
            MethodInfo method = this.GetType().GetMethod("TestJob_MultipleListenerSingletons", BindingFlags.Static | BindingFlags.NonPublic);
            var descriptor = FunctionIndexer.FromMethod(method, _configuration);

            NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            {
                SingletonManager.GetListenerSingletonOrNull(typeof(TestListener), descriptor);
            });
            Assert.Equal("Only one SingletonAttribute using mode 'Listener' is allowed.", exception.Message);
        }

        [Fact]
        public void GetListenerSingletonOrNull_MethodSingletonTakesPrecedence()
        {
            MethodInfo method = this.GetType().GetMethod("TestJob_ListenerSingleton", BindingFlags.Static | BindingFlags.NonPublic);
            var descriptor = FunctionIndexer.FromMethod(method, _configuration);

            SingletonAttribute attribute = SingletonManager.GetListenerSingletonOrNull(typeof(TestListener), descriptor);
            Assert.Equal("Function", attribute.ScopeId);
        }

        [Fact]
        public void GetListenerSingletonOrNull_ReturnsListenerClassSingleton()
        {
            MethodInfo method = this.GetType().GetMethod("TestJob", BindingFlags.Static | BindingFlags.NonPublic);
            var descriptor = FunctionIndexer.FromMethod(method, _configuration);

            SingletonAttribute attribute = SingletonManager.GetListenerSingletonOrNull(typeof(TestListener), descriptor);
            Assert.Equal("Listener", attribute.ScopeId);
        }

        [Theory]
        [InlineData(SingletonMode.Function)]
        [InlineData(SingletonMode.Listener)]
        public void ValidateSingletonAttribute_ScopeIsHost_ScopeIdEmpty_Throws(SingletonMode mode)
        {
            SingletonAttribute attribute = new SingletonAttribute(null, SingletonScope.Host);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            {
                SingletonManager.ValidateSingletonAttribute(attribute, mode);
            });
            Assert.Equal("A ScopeId value must be provided when using scope 'Host'.", exception.Message);
        }

        [Fact]
        public void ValidateSingletonAttribute_ScopeIsHost_ModeIsListener_Throws()
        {
            SingletonAttribute attribute = new SingletonAttribute("TestScope", SingletonScope.Host);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            {
                SingletonManager.ValidateSingletonAttribute(attribute, SingletonMode.Listener);
            });
            Assert.Equal("Scope 'Host' cannot be used when the mode is set to 'Listener'.", exception.Message);
        }
        [Fact]
        public void GetLockPeriod_ReturnsExpectedValue()
        {
            SingletonAttribute attribute = new SingletonAttribute
            {
                Mode = SingletonMode.Listener
            };
            var config = new SingletonOptions()
            {
                LockPeriod = TimeSpan.FromSeconds(16),
                ListenerLockPeriod = TimeSpan.FromSeconds(17)
            };

            TimeSpan value = SingletonManager.GetLockPeriod(attribute, config);
            Assert.Equal(config.ListenerLockPeriod, value);

            attribute.Mode = SingletonMode.Function;
            value = SingletonManager.GetLockPeriod(attribute, config);
            Assert.Equal(config.LockPeriod, value);
        }

        [Fact]
        public void GetLockAcquisitionTimeout_ReturnsExpectedValue()
        {
            // override via attribute
            var method = GetType().GetMethod("TestJob_LockAcquisitionTimeoutOverride", BindingFlags.Static | BindingFlags.NonPublic);
            var attribute = method.GetCustomAttribute<SingletonAttribute>();
            var config = new SingletonOptions();
            var result = SingletonManager.GetLockAcquisitionTimeout(attribute, config);
            Assert.Equal(TimeSpan.FromSeconds(5), result);

            // when not set via attribute, defaults to config value
            attribute = new SingletonAttribute();
            config.LockAcquisitionTimeout = TimeSpan.FromSeconds(3);
            result = SingletonManager.GetLockAcquisitionTimeout(attribute, config);
            Assert.Equal(config.LockAcquisitionTimeout, result);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RenewLeaseCommand_ComputesNextDelay_BasedOnRenewalResult(bool renewalSucceeded)
        {
            var lockManagerMock = new Mock<IDistributedLockManager>(MockBehavior.Strict);
            var lockMock = new Mock<IDistributedLock>(MockBehavior.Strict);
            var delayStrategyMock = new Mock<IDelayStrategy>(MockBehavior.Strict);
            var cancellationToken = new CancellationToken();
            var delay = TimeSpan.FromMilliseconds(33);

            lockManagerMock.Setup(p => p.RenewAsync(lockMock.Object, cancellationToken)).ReturnsAsync(renewalSucceeded);
            delayStrategyMock.Setup(p => p.GetNextDelay(renewalSucceeded)).Returns(delay);

            var command = new SingletonManager.RenewLeaseCommand(lockManagerMock.Object, lockMock.Object, delayStrategyMock.Object);
            var result = await command.ExecuteAsync(cancellationToken);

            await result.Wait;

            lockManagerMock.VerifyAll();
            delayStrategyMock.VerifyAll();
        }

        private static void TestJob()
        {
        }

        [Singleton("Function", Mode = SingletonMode.Function, LockAcquisitionTimeout = 5)]
        private static void TestJob_LockAcquisitionTimeoutOverride()
        {
        }

        [Singleton("Function", Mode = SingletonMode.Listener)]
        private static void TestJob_ListenerSingleton()
        {
        }

        [Singleton("bar")]
        [Singleton("foo")]
        private static void TestJob_MultipleFunctionSingletons()
        {
        }

        [Singleton("bar", Mode = SingletonMode.Listener)]
        [Singleton("foo", Mode = SingletonMode.Listener)]
        private static void TestJob_MultipleListenerSingletons()
        {
        }

        [Singleton("Listener", Mode = SingletonMode.Listener)]
        private class TestListener
        {
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
