// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FakeStorage;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
using Xunit;
using SingletonLockHandle = Microsoft.Azure.WebJobs.Host.StorageBaseDistributedLockManager.SingletonLockHandle;

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

        private StorageBaseDistributedLockManager _core;
        private SingletonManager _singletonManager;
        private SingletonOptions _singletonConfig;

        private CloudBlobDirectory _mockBlobDirectory;
        private CloudBlobDirectory _mockSecondaryBlobDirectory;

        internal FakeAccount _account1 = new FakeAccount();
        internal FakeAccount _account2 = new FakeAccount();

        private Mock<IWebJobsExceptionHandler> _mockExceptionDispatcher;
        private Mock<CloudBlockBlob> _mockStorageBlob;
        private TestLoggerProvider _loggerProvider;
        private readonly Dictionary<string, string> _mockBlobMetadata;
        private TestNameResolver _nameResolver;

        private class FakeLeaseProvider : StorageBaseDistributedLockManager
        {
            internal FakeAccount _account1 = new FakeAccount();
            internal FakeAccount _account2 = new FakeAccount();

            public FakeLeaseProvider(ILoggerFactory logger) : base(logger) { }

            protected override CloudBlobContainer GetContainer(string accountName)
            {
                FakeAccount account;
                if (string.IsNullOrEmpty(accountName) || accountName == ConnectionStringNames.Storage)
                {
                    account = _account1;
                }
                else if (accountName == Secondary)
                {
                    account = _account2;
                }
                else
                {
                    throw new InvalidOperationException("Unknown account: " + accountName);
                }
                var container = account.CreateCloudBlobClient().GetContainerReference("azure-webjobs-hosts");
                return container;
            }
        }

        public SingletonManagerTests()
        {
            ILoggerFactory loggerFactory = new LoggerFactory();
            _loggerProvider = new TestLoggerProvider();
            loggerFactory.AddProvider(_loggerProvider);

            var logger = loggerFactory?.CreateLogger(LogCategories.Singleton);


            var leaseProvider = new FakeLeaseProvider(loggerFactory);
            _mockBlobDirectory = leaseProvider._account1.CreateCloudBlobClient().GetContainerReference(HostContainerNames.Hosts).GetDirectoryReference(HostDirectoryNames.SingletonLocks);
            _mockSecondaryBlobDirectory = leaseProvider._account2.CreateCloudBlobClient().GetContainerReference(HostContainerNames.Hosts).GetDirectoryReference(HostDirectoryNames.SingletonLocks);

            _mockExceptionDispatcher = new Mock<IWebJobsExceptionHandler>(MockBehavior.Strict);

            _mockStorageBlob = new Mock<CloudBlockBlob>(MockBehavior.Strict,
                new Uri("https://fakeaccount.blob.core.windows.net/" + HostContainerNames.Hosts + "/" + HostDirectoryNames.SingletonLocks + "/" + TestLockId));

            _mockBlobMetadata = new Dictionary<string, string>();
            leaseProvider._account1.SetBlob(HostContainerNames.Hosts, HostDirectoryNames.SingletonLocks + "/" + TestLockId, _mockStorageBlob.Object);


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
        public void GetLockDirectory_HandlesMultipleAccounts()
        {
            var directory = _core.GetLockDirectory(ConnectionStringNames.Storage);
            Assert.Equal(_mockBlobDirectory.Uri, directory.Uri);

            directory = _core.GetLockDirectory(null);
            Assert.Equal(_mockBlobDirectory.Uri, directory.Uri);

            directory = _core.GetLockDirectory(Secondary);
            Assert.Equal(_mockSecondaryBlobDirectory.Uri, directory.Uri);
        }

        [Fact]
        public async Task TryLockAsync_CreatesBlob_WhenItDoesNotExist()
        {
            CancellationToken cancellationToken = new CancellationToken();
            RequestResult storageResult = new RequestResult
            {
                HttpStatusCode = 404
            };
            StorageException storageException = new StorageException(storageResult, null, null);

            int count = 0;

            MockAcquireLeaseAsync(null, () =>
            {
                if (count++ == 0)
                {
                    throw storageException;
                }
                return TestLeaseId;
            });

            _mockStorageBlob.Setup(p => p.UploadTextAsync(string.Empty, null, null, null, null, cancellationToken)).Returns(Task.FromResult(true));
            //_mockStorageBlob.SetupGet(p => p.Metadata).Returns(_mockBlobMetadata);
            _mockStorageBlob.Setup(p => p.SetMetadataAsync(It.Is<AccessCondition>(q => q.LeaseId == TestLeaseId), null, null, cancellationToken)).Returns(Task.FromResult(true));
            _mockStorageBlob.Setup(p => p.ReleaseLeaseAsync(It.Is<AccessCondition>(q => q.LeaseId == TestLeaseId), null, null, cancellationToken)).Returns(Task.FromResult(true));

            SingletonAttribute attribute = new SingletonAttribute();
            RenewableLockHandle lockHandle = await _singletonManager.TryLockAsync(TestLockId, TestInstanceId, attribute, cancellationToken);
            var innerHandle = lockHandle.GetInnerHandle();

            Assert.Equal(_mockStorageBlob.Object, innerHandle.Blob);
            Assert.Equal(TestLeaseId, innerHandle.LeaseId);
            Assert.Equal(1, _mockStorageBlob.Object.Metadata.Keys.Count);
            Assert.Equal(_mockStorageBlob.Object.Metadata[StorageBaseDistributedLockManager.FunctionInstanceMetadataKey], TestInstanceId);
        }

        [Fact]
        public async Task TryLockAsync_CreatesBlobLease_WithAutoRenewal()
        {
            CancellationToken cancellationToken = new CancellationToken();
            //_mockStorageBlob.SetupGet(p => p.Metadata).Returns(_mockBlobMetadata);

            MockAcquireLeaseAsync(null, () => TestLeaseId);

            _mockStorageBlob.Setup(p => p.SetMetadataAsync(It.Is<AccessCondition>(q => q.LeaseId == TestLeaseId), null, null, cancellationToken)).Returns(Task.FromResult(true));
            _mockStorageBlob.Setup(p => p.ReleaseLeaseAsync(It.Is<AccessCondition>(q => q.LeaseId == TestLeaseId), null, null, cancellationToken)).Returns(Task.FromResult(true));

            int renewCount = 0;
            _mockStorageBlob.Setup(p => p.RenewLeaseAsync(It.Is<AccessCondition>(q => q.LeaseId == TestLeaseId), null, null, It.IsAny<CancellationToken>()))
                .Callback<AccessCondition, BlobRequestOptions, OperationContext, CancellationToken>(
                (mockAccessCondition, mockOptions, mockContext, mockCancellationToken) =>
                {
                    renewCount++;
                }).Returns(Task.FromResult(true));

            SingletonAttribute attribute = new SingletonAttribute();
            var lockHandle = await _singletonManager.TryLockAsync(TestLockId, TestInstanceId, attribute, cancellationToken);
            var innerHandle = lockHandle.GetInnerHandle();

            Assert.Equal(_mockStorageBlob.Object, innerHandle.Blob);
            Assert.Equal(TestLeaseId, innerHandle.LeaseId);
            Assert.Equal(1, _mockStorageBlob.Object.Metadata.Keys.Count);
            Assert.Equal(_mockStorageBlob.Object.Metadata[StorageBaseDistributedLockManager.FunctionInstanceMetadataKey], TestInstanceId);

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

            _mockStorageBlob.VerifyAll();
        }

        [Fact]
        public async Task TryLockAsync_WithContention_PollsForLease()
        {
            CancellationToken cancellationToken = new CancellationToken();
            // _mockStorageBlob.SetupGet(p => p.Metadata).Returns(_mockBlobMetadata);
            _mockStorageBlob.Setup(p => p.SetMetadataAsync(It.Is<AccessCondition>(q => q.LeaseId == TestLeaseId), null, null, cancellationToken)).Returns(Task.FromResult(true));

            int numRetries = 3;
            int count = 0;

            MockAcquireLeaseAsync(null, () =>
            {
                count++;
                return count > numRetries ? TestLeaseId : null;
            });

            SingletonAttribute attribute = new SingletonAttribute();
            var lockHandle = await _singletonManager.TryLockAsync(TestLockId, TestInstanceId, attribute, cancellationToken);
            var innerHandle = lockHandle.GetInnerHandle();

            Assert.NotNull(lockHandle);
            Assert.Equal(TestLeaseId, innerHandle.LeaseId);
            Assert.Equal(numRetries, count - 1);
            Assert.NotNull(lockHandle.LeaseRenewalTimer);

            _mockStorageBlob.VerifyAll();
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
            SingletonLockHandle lockHandle = await _singletonManager.TryLockInternalAsync(TestLockId, TestInstanceId, attribute, cancellationToken, retry: false);

            Assert.Null(lockHandle);
            Assert.Equal(1, count);

            _mockStorageBlob.VerifyAll();
        }

        // Helper to setup mock since the signatures are very complex
        private void MockAcquireLeaseAsync(Action fpAction, Func<string> returns)
        {
            _mockStorageBlob.Setup(
                p => p.AcquireLeaseAsync(_singletonConfig.LockPeriod, null, It.IsAny<AccessCondition>(), It.IsAny<BlobRequestOptions>(), It.IsAny<OperationContext>(), It.IsAny<CancellationToken>())
                )
                .Callback<TimeSpan?, string, AccessCondition, BlobRequestOptions, OperationContext, CancellationToken>(
                (mockPeriod, mockLeaseId, accessCondition, blobRequest, opCtx, cancelToken) =>
                {
                    fpAction?.Invoke();
                }).Returns(() =>
                {
                    var retResult = returns();
                    return Task.FromResult<string>(retResult);
                });
        }

        private void MockFetchAttributesAsync(Action fpAction)
        {
            _mockStorageBlob.Setup(
                p => p.FetchAttributesAsync(It.IsAny<AccessCondition>(), It.IsAny<BlobRequestOptions>(), It.IsAny<OperationContext>(), It.IsAny<CancellationToken>())
                )
                .Callback<AccessCondition, BlobRequestOptions, OperationContext, CancellationToken>(
                (accessCondition, blobRequest, opCtx, cancelToken) =>
                {
                    fpAction?.Invoke();
                }).Returns(() =>
                {
                    return Task.CompletedTask;
                });
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

            _mockStorageBlob.VerifyAll();
        }

        [Fact]
        public async Task ReleaseLockAsync_StopsRenewalTimerAndReleasesLease()
        {
            CancellationToken cancellationToken = new CancellationToken();

            Mock<ITaskSeriesTimer> mockRenewalTimer = new Mock<ITaskSeriesTimer>(MockBehavior.Strict);
            mockRenewalTimer.Setup(p => p.StopAsync(cancellationToken)).Returns(Task.FromResult(true));

            _mockStorageBlob.Setup(p => p.ReleaseLeaseAsync(It.Is<AccessCondition>(q => q.LeaseId == TestLeaseId), null, null, cancellationToken)).Returns(Task.FromResult(true));

            var handle = new RenewableLockHandle(
                new SingletonLockHandle
                {
                    Blob = _mockStorageBlob.Object,
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
            MockFetchAttributesAsync(null);

            _mockStorageBlob.Object.Properties.SetLeaseState(LeaseState.Leased);

            SingletonAttribute attribute = new SingletonAttribute();
            string lockOwner = await _singletonManager.GetLockOwnerAsync(attribute, TestLockId, CancellationToken.None);
            Assert.Equal(null, lockOwner);

            _mockStorageBlob.Object.Metadata.Add(StorageBaseDistributedLockManager.FunctionInstanceMetadataKey, TestLockId);
            lockOwner = await _singletonManager.GetLockOwnerAsync(attribute, TestLockId, CancellationToken.None);
            Assert.Equal(TestLockId, lockOwner);

            _mockStorageBlob.VerifyAll();
        }

        [Fact]
        public async Task GetLockOwnerAsync_LeaseAvailable_ReturnsNull()
        {
            MockFetchAttributesAsync(null);

            _mockStorageBlob.Object.Properties.SetLeaseState(LeaseState.Available);
            _mockStorageBlob.Object.Properties.SetLeaseStatus(LeaseStatus.Unlocked);

            SingletonAttribute attribute = new SingletonAttribute();
            string lockOwner = await _singletonManager.GetLockOwnerAsync(attribute, TestLockId, CancellationToken.None);
            Assert.Equal(null, lockOwner);

            _mockStorageBlob.VerifyAll();
        }

        [Theory]
        [InlineData(SingletonScope.Function, null, "TestHostId/Microsoft.Azure.WebJobs.Host.UnitTests.Singleton.SingletonManagerTests.TestJob")]
        [InlineData(SingletonScope.Function, "", "TestHostId/Microsoft.Azure.WebJobs.Host.UnitTests.Singleton.SingletonManagerTests.TestJob")]
        [InlineData(SingletonScope.Function, "testscope", "TestHostId/Microsoft.Azure.WebJobs.Host.UnitTests.Singleton.SingletonManagerTests.TestJob.testscope")]
        [InlineData(SingletonScope.Host, "testscope", "TestHostId/testscope")]
        public void FormatLockId_ReturnsExpectedValue(SingletonScope scope, string scopeId, string expectedLockId)
        {
            MethodInfo methodInfo = this.GetType().GetMethod("TestJob", BindingFlags.Static | BindingFlags.NonPublic);
            var descriptor = FunctionIndexer.FromMethod(methodInfo);
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
            var descriptor = FunctionIndexer.FromMethod(method);

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
            var descriptor = FunctionIndexer.FromMethod(method);

            SingletonAttribute attribute = SingletonManager.GetListenerSingletonOrNull(typeof(TestListener), descriptor);
            Assert.Equal("Function", attribute.ScopeId);
        }

        [Fact]
        public void GetListenerSingletonOrNull_ReturnsListenerClassSingleton()
        {
            MethodInfo method = this.GetType().GetMethod("TestJob", BindingFlags.Static | BindingFlags.NonPublic);
            var descriptor = FunctionIndexer.FromMethod(method);

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
