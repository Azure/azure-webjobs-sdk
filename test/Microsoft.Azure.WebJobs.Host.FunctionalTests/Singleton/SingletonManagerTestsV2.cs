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
using Microsoft.Azure.WebJobs.Host.FunctionalTests;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using SingletonLockHandle = Microsoft.Azure.WebJobs.Host.GenericDistributedLockManager.SingletonLockHandle;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Singleton
{
    public class SingletonManagerTestsV2
    {
        private const string TestHostId = "testhost";
        private const string TestLockId = "testid";
        private const string TestInstanceId = "testinstance";
        private const string TestLeaseId = "testleaseid";
        private const string Secondary = "SecondaryStorage";

        private readonly IConfiguration _configuration = new ConfigurationBuilder().Build();

        private GenericDistributedLockManager _core;
        private SingletonManager _singletonManager;
        private SingletonOptions _singletonConfig;

        private Mock<IWebJobsExceptionHandler> _mockExceptionDispatcher;

        private readonly Mock<BlobContainerClient> _mockBlobContainerClient;
        private readonly Mock<BlobClient> _mockStorageBlobClient;
        private readonly Mock<BlobLeaseClient> _mockBlobLeaseClient;
        private readonly BlobProperties _testBlobProperties;
        private readonly Dictionary<string, string> _mockBlobMetadata;

        private TestLoggerProvider _loggerProvider;
        private TestNameResolver _nameResolver;

        private class TestLeaseProviderFactory : ILeaseProviderFactory
        {
            // mocked
            public BlobContainerClient _blobContainerClient;
            public BlobLeaseClient _blobLeaseClient;

            public ILeaseProvider GetLeaseProvider(string lockId, string accountOverride = null)
            {
                return new TestLeaseProvider(lockId, _blobContainerClient, _blobLeaseClient);
            }
        }

        private class TestLeaseProvider : AzureBlobLeaseProvider
        {
            // mocked
            public BlobLeaseClient _blobLeaseClient;

            public TestLeaseProvider(string lockId, BlobContainerClient blobContainerClient, BlobLeaseClient blobLeaseClient) : base(lockId, blobContainerClient) 
            {
                _blobLeaseClient = blobLeaseClient;
            }

            protected override BlobLeaseClient GetBlobLeaseClient(BlobClient blobClient, string proposedLeaseId)
            {
                return _blobLeaseClient;
            }
        }

        //private class FakeLeaseProvider : StorageBaseDistributedLockManager
        //{
        //    // To set up testing
        //    public BlobContainerClient _containerClient;
        //    public BlobLeaseClient _blobLeaseClient;

        //    public FakeLeaseProvider(ILoggerFactory logger) : base(logger) { }

        //    protected override BlobContainerClient GetContainerClient(string accountName)
        //    {
        //        if (!string.IsNullOrWhiteSpace(accountName))
        //        {
        //            throw new InvalidOperationException("Must replace singleton lease manager to support multiple accounts");
        //        }

        //        return _containerClient;
        //    }

        //    // this will be mocked
        //    protected override BlobLeaseClient GetBlobLeaseClient(BlobClient blobClient, string proposedLeaseId)
        //    {
        //        return _blobLeaseClient;
        //    }
        //}

        public SingletonManagerTestsV2()
        {
            ILoggerFactory loggerFactory = new LoggerFactory();
            _loggerProvider = new TestLoggerProvider();
            loggerFactory.AddProvider(_loggerProvider);

            var logger = loggerFactory?.CreateLogger(LogCategories.Singleton);

            var testLeaseProviderFactory = new TestLeaseProviderFactory();

            _mockExceptionDispatcher = new Mock<IWebJobsExceptionHandler>(MockBehavior.Strict);

            // Setting up Blob storage structure
            // mockBlobContainerClient -> mockStorageBlobClient -> *has* testBlobProperties -> *contains* _mockBlobMetadata
            _mockStorageBlobClient = new Mock<BlobClient>(MockBehavior.Strict,
                new Uri("https://fakeaccount.blob.core.windows.net/" + HostContainerNames.Hosts + "/" + HostDirectoryNames.SingletonLocks + "/" + TestLockId), null);

            _mockBlobMetadata = new Dictionary<string, string>();
            _testBlobProperties = new BlobProperties();
            _testBlobProperties.SetInternalProperty("Metadata", _mockBlobMetadata);

            _mockBlobContainerClient = new Mock<BlobContainerClient>(MockBehavior.Strict, new Uri("https://fakeaccount.blob.core.windows.net/" + HostContainerNames.Hosts), null);
            _mockBlobContainerClient.Setup(p => p.GetBlobClient(HostDirectoryNames.SingletonLocks + "/" + TestLockId)).Returns(_mockStorageBlobClient.Object);
            testLeaseProviderFactory._blobContainerClient = _mockBlobContainerClient.Object;

            // mockStorageBlobClient -> *has* mockBlobLeaseClient
            _mockBlobLeaseClient = new Mock<BlobLeaseClient>(MockBehavior.Strict, _mockStorageBlobClient.Object, null);
            testLeaseProviderFactory._blobLeaseClient = _mockBlobLeaseClient.Object;
            // End setting up Blob storage structure

            _singletonConfig = new SingletonOptions();

            // use reflection to bypass the normal validations (so tests can run fast)
            TestHelpers.SetField(_singletonConfig, "_lockAcquisitionPollingInterval", TimeSpan.FromMilliseconds(25));
            TestHelpers.SetField(_singletonConfig, "_lockPeriod", TimeSpan.FromMilliseconds(500));
            _singletonConfig.LockAcquisitionTimeout = TimeSpan.FromMilliseconds(200);

            _nameResolver = new TestNameResolver();

            _core = new GenericDistributedLockManager(loggerFactory, testLeaseProviderFactory);

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

        [Fact]
        public async Task TryLockAsync_CreatesBlob_WhenItDoesNotExist()
        {
            CancellationToken cancellationToken = new CancellationToken();
            RequestFailedException storageException = new RequestFailedException(404, "Mocking not found exception");

            int count = 0;

            MockAcquireLeaseAsync(null, () =>
            {
                if (count++ == 0)
                {
                    throw storageException;
                }
                return TestLeaseId;
            });

            _mockBlobContainerClient.Setup(p => p.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), cancellationToken))
                .Returns(Task.FromResult(new Mock<Response<BlobContainerInfo>>().Object));
            _mockStorageBlobClient.Setup(p => p.UploadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new Mock<Response<BlobContentInfo>>().Object));
            MockFetchAttributesAsync(null);
            _mockStorageBlobClient.Setup(p => p.SetMetadataAsync(It.IsAny<Dictionary<string, string>>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new Mock<Response<BlobInfo>>().Object));
            _mockBlobLeaseClient.Setup(p => p.ReleaseAsync(It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new Mock<Response<ReleasedObjectInfo>>().Object));

            SingletonAttribute attribute = new SingletonAttribute();
            RenewableLockHandle lockHandle = await _singletonManager.TryLockAsync(TestLockId, TestInstanceId, attribute, cancellationToken);
            var innerHandle = (SingletonLockHandle)lockHandle.GetInnerHandle();

            Assert.Equal(TestLeaseId, innerHandle.LeaseId);
            Assert.Single(_mockBlobMetadata.Keys);
            Assert.Equal(_mockBlobMetadata[StorageBaseDistributedLockManager.FunctionInstanceMetadataKey], TestInstanceId);
        }

        [Fact]
        public async Task TryLockAsync_CreatesBlobLease_WithAutoRenewal()
        {
            CancellationToken cancellationToken = new CancellationToken();
            //_mockStorageBlob.SetupGet(p => p.Metadata).Returns(_mockBlobMetadata);

            MockAcquireLeaseAsync(null, () => TestLeaseId);
            MockFetchAttributesAsync(null);
            _mockStorageBlobClient.Setup(p => p.SetMetadataAsync(It.IsAny<Dictionary<string, string>>(), It.Is<BlobRequestConditions>(q => q.LeaseId == TestLeaseId), cancellationToken))
                .Returns(Task.FromResult(new Mock<Response<BlobInfo>>().Object));
            _mockBlobLeaseClient.Setup(p => p.ReleaseAsync(It.IsAny<RequestConditions>(), cancellationToken))
                .Returns(Task.FromResult(new Mock<Response<ReleasedObjectInfo>>().Object));

            int renewCount = 0;
            _mockBlobLeaseClient.Setup(p => p.RenewAsync(It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>()))
                .Callback<RequestConditions, CancellationToken>(
                (mockConditions, mockCancellationToken) =>
                {
                    renewCount++;
                })
                .Returns(Task.FromResult(new Mock<Response<BlobLease>>().Object));

            SingletonAttribute attribute = new SingletonAttribute();
            var lockHandle = await _singletonManager.TryLockAsync(TestLockId, TestInstanceId, attribute, cancellationToken);
            var innerHandle = (SingletonLockHandle)lockHandle.GetInnerHandle();

            Assert.Equal(TestLeaseId, innerHandle.LeaseId);
            Assert.Single(_mockBlobMetadata.Keys);
            Assert.Equal(_mockBlobMetadata[StorageBaseDistributedLockManager.FunctionInstanceMetadataKey], TestInstanceId);

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

            _mockStorageBlobClient.VerifyAll();
        }

        [Fact]
        public async Task TryLockAsync_WithContention_PollsForLease()
        {
            CancellationToken cancellationToken = new CancellationToken();

            MockFetchAttributesAsync(null);
            _mockStorageBlobClient.Setup(p => p.SetMetadataAsync(It.IsAny<Dictionary<string, string>>(), It.Is<BlobRequestConditions>(q => q.LeaseId == TestLeaseId), cancellationToken))
                .Returns(Task.FromResult(new Mock<Response<BlobInfo>>().Object));

            int numRetries = 3;
            int count = 0;

            MockAcquireLeaseAsync(null, () =>
            {
                count++;
                return count > numRetries ? TestLeaseId : null;
            });

            SingletonAttribute attribute = new SingletonAttribute();
            var lockHandle = await _singletonManager.TryLockAsync(TestLockId, TestInstanceId, attribute, cancellationToken);
            var innerHandle = (SingletonLockHandle)lockHandle.GetInnerHandle();

            Assert.NotNull(lockHandle);
            Assert.Equal(TestLeaseId, innerHandle.LeaseId);
            Assert.Equal(numRetries, count - 1);
            Assert.NotNull(lockHandle.LeaseRenewalTimer);

            _mockStorageBlobClient.VerifyAll();
        }

        [Fact]
        public async Task TryLockAsync_WithContention_NoRetry_DoesNotPollForLease()
        {
            CancellationToken cancellationToken = new CancellationToken();

            int count = 0;

            MockAcquireLeaseAsync(null, () =>
            {
                count++;
                return null;
            });

            SingletonAttribute attribute = new SingletonAttribute();
            SingletonLockHandle lockHandle = (SingletonLockHandle)await _singletonManager.TryLockInternalAsync(TestLockId, TestInstanceId, attribute, cancellationToken, retry: false);

            Assert.Null(lockHandle);
            Assert.Equal(1, count);

            _mockStorageBlobClient.VerifyAll();
        }

        // Helper to setup mock since the signatures are very complex
        private void MockAcquireLeaseAsync(Action fpAction, Func<string> returns)
        {
            _mockBlobLeaseClient.Setup(p => p.AcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>()))
                .Callback<TimeSpan?, RequestConditions, CancellationToken>((mockPeriod, requestCondition, cancellationToken) =>
                {
                    fpAction?.Invoke();
                })
                .Returns(() =>
                {
                    var blobLease = FunctionalTests.Utility.Construct<BlobLease>(null, null);
                    blobLease.SetInternalProperty("LeaseId", returns());
                    var mockResponse = new Mock<Response<BlobLease>>();
                    mockResponse.SetupGet(p => p.Value).Returns(blobLease);
                    return Task.FromResult(mockResponse.Object);
                });
        }

        private void MockFetchAttributesAsync(Action fpAction)
        {
            var mockResponse = new Mock<Response<BlobProperties>>();
            mockResponse.SetupGet(p => p.Value).Returns(_testBlobProperties);
            _mockStorageBlobClient.Setup(
                p => p.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
                .Callback<BlobRequestConditions, CancellationToken>((requestCondition, cancellationToken) =>
                {
                    fpAction?.Invoke();
                })
                .Returns(Task.FromResult(mockResponse.Object));
        }

        [Fact]
        public async Task LockAsync_WithContention_AcquisitionTimeoutExpires_Throws()
        {
            CancellationToken cancellationToken = new CancellationToken();

            int count = 0;

            MockAcquireLeaseAsync(() =>
            {
                ++count;
            }, () => null);

            SingletonAttribute attribute = new SingletonAttribute();
            TimeoutException exception = await Assert.ThrowsAsync<TimeoutException>(async () => await _singletonManager.LockAsync(TestLockId, TestInstanceId, attribute, cancellationToken));

            int expectedRetryCount = (int)(_singletonConfig.LockAcquisitionTimeout.TotalMilliseconds / _singletonConfig.LockAcquisitionPollingInterval.TotalMilliseconds);
            Assert.Equal(expectedRetryCount, count - 1);
            Assert.Equal("Unable to acquire singleton lock blob lease for blob 'testid' (timeout of 0:00:00.2 exceeded).", exception.Message);

            _mockStorageBlobClient.VerifyAll();
        }

        [Fact]
        public async Task ReleaseLockAsync_StopsRenewalTimerAndReleasesLease()
        {
            CancellationToken cancellationToken = new CancellationToken();

            Mock<ITaskSeriesTimer> mockRenewalTimer = new Mock<ITaskSeriesTimer>(MockBehavior.Strict);
            mockRenewalTimer.Setup(p => p.StopAsync(cancellationToken)).Returns(Task.FromResult(true));

            _mockBlobLeaseClient.Setup(p => p.ReleaseAsync(It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new Mock<Response<ReleasedObjectInfo>>().Object));

            var testLeaseProvider = new TestLeaseProvider(TestLockId, _mockBlobContainerClient.Object, _mockBlobLeaseClient.Object);
            var handle = new RenewableLockHandle(
                new SingletonLockHandle
                {
                    LeaseProvider = testLeaseProvider,
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
            _testBlobProperties.SetInternalProperty("LeaseState", LeaseState.Leased);

            MockFetchAttributesAsync(null);

            SingletonAttribute attribute = new SingletonAttribute();
            string lockOwner = await _singletonManager.GetLockOwnerAsync(attribute, TestLockId, CancellationToken.None);
            Assert.Equal(null, lockOwner);

            _mockBlobMetadata.Add(StorageBaseDistributedLockManager.FunctionInstanceMetadataKey, TestLockId);
            lockOwner = await _singletonManager.GetLockOwnerAsync(attribute, TestLockId, CancellationToken.None);
            Assert.Equal(TestLockId, lockOwner);

            _mockStorageBlobClient.VerifyAll();
        }

        [Fact]
        public async Task GetLockOwnerAsync_LeaseAvailable_ReturnsNull()
        {
            _testBlobProperties.SetInternalProperty("LeaseState", LeaseState.Available);
            _testBlobProperties.SetInternalProperty("LeaseStatus", LeaseStatus.Unlocked);

            MockFetchAttributesAsync(null);

            SingletonAttribute attribute = new SingletonAttribute();
            string lockOwner = await _singletonManager.GetLockOwnerAsync(attribute, TestLockId, CancellationToken.None);
            Assert.Equal(null, lockOwner);

            _mockStorageBlobClient.VerifyAll();
        }

        [Theory]
        [InlineData(SingletonScope.Function, null, "TestHostId/Microsoft.Azure.WebJobs.Host.UnitTests.Singleton.SingletonManagerTestsV2.TestJob")]
        [InlineData(SingletonScope.Function, "", "TestHostId/Microsoft.Azure.WebJobs.Host.UnitTests.Singleton.SingletonManagerTestsV2.TestJob")]
        [InlineData(SingletonScope.Function, "testscope", "TestHostId/Microsoft.Azure.WebJobs.Host.UnitTests.Singleton.SingletonManagerTestsV2.TestJob.testscope")]
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
