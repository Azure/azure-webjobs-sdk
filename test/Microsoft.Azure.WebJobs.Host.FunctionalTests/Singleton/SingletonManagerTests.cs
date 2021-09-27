// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.FunctionalTests;
using Microsoft.Azure.WebJobs.Host.FunctionalTests.TestDoubles;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using SingletonLockHandle = Microsoft.Azure.WebJobs.Host.BlobLeaseDistributedLockManager.SingletonLockHandle;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Singleton
{
    internal static class Ext // $$$ move to better place
    {
        public static SingletonLockHandle GetInnerHandle(this RenewableLockHandle handle)
        {
            if (handle == null)
            {
                return null;
            }
            return (SingletonLockHandle)handle.InnerLock;
        }
    }

    public class SingletonManagerTests : IClassFixture<SingletonManagerTests.TestFixture>
    {
        private const string TestArtifactContainerPrefix = "e2e-singletonmanagertests";
        private const string TestArtifactContainerName = TestArtifactContainerPrefix + "-%rnd%";

        private const string TestHostId = "testhost";
        private const string TestLockId = "testid";
        private const string TestInstanceId = "testinstance";

        private readonly IConfiguration _configuration = new ConfigurationBuilder().Build();

        private BlobLeaseDistributedLockManager _core;
        private SingletonManager _singletonManager;
        private SingletonOptions _singletonConfig;

        private Mock<IWebJobsExceptionHandler> _mockExceptionDispatcher;

        private readonly BlobContainerClient _testContainerClient;

        private TestLoggerProvider _loggerProvider;
        private TestNameResolver _nameResolver;

        private class TestBlobLeaseDistributedLockManager : BlobLeaseDistributedLockManager
        {
            // To set up testing
            public BlobContainerClient ContainerClient;

            public TestBlobLeaseDistributedLockManager(ILoggerFactory logger, IAzureStorageProvider azureStorageProvider) : base(logger, azureStorageProvider) { }

            protected override BlobContainerClient GetContainerClient(string accountName)
            {
                if (!string.IsNullOrWhiteSpace(accountName))
                {
                    throw new InvalidOperationException("Must replace singleton lease manager to support multiple accounts");
                }

                return ContainerClient;
            }
        }

        // All dependencies of SingletonManager is mocked except Blob storage
        public SingletonManagerTests()
        {
            ILoggerFactory loggerFactory = new LoggerFactory();
            _loggerProvider = new TestLoggerProvider();
            loggerFactory.AddProvider(_loggerProvider);

            var testBlobLeaseDistributedLockManager = new TestBlobLeaseDistributedLockManager(loggerFactory, null);
            _testContainerClient = TestFixture.GetTestContainerClient(new RandomNameResolver().ResolveInString(TestArtifactContainerName));
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
            await Assert.ThrowsAnyAsync<Exception>(async () => await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(15)));

            // Checks Instance Id in metadata
            var metadata = (await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetPropertiesAsync()).Value.Metadata;
            Assert.Single(metadata.Keys);
            Assert.Equal(TestInstanceId, metadata[BlobLeaseDistributedLockManager.FunctionInstanceMetadataKey]);
            Assert.NotNull(lockHandle);
            Assert.NotNull(lockHandle.LeaseRenewalTimer);
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
            await Assert.ThrowsAnyAsync<Exception>(async () => await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(15)));

            // Checks Instance Id in metadata
            var metadata = (await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetPropertiesAsync()).Value.Metadata;
            Assert.Single(metadata.Keys);
            Assert.Equal(TestInstanceId, metadata[BlobLeaseDistributedLockManager.FunctionInstanceMetadataKey]);
            Assert.NotNull(lockHandle);
            Assert.NotNull(lockHandle.LeaseRenewalTimer);
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
            await Assert.ThrowsAnyAsync<Exception>(async () => await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(15)));

            // wait for enough time that we expect some lease renewals to occur
            var duration = _singletonConfig.LockPeriod.TotalMilliseconds + TimeSpan.FromSeconds(1).TotalMilliseconds;
            await Task.Delay((int)duration);

            // Shouldn't be able to lease this blob yet
            await Assert.ThrowsAnyAsync<Exception>(async () => await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(15)));

            // now release the lock and verify no more renewals
            await _singletonManager.ReleaseLockAsync(lockHandle, cancellationToken);

            // Should be able to lease this blob now
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

            await _testContainerClient.CreateIfNotExistsAsync();
            if (!_testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).Exists())
            {
                await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).UploadTextAsync("");
            }

            // Lease this blob to test polling behavior
            await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(15));

            SingletonAttribute attribute = new SingletonAttribute();
            var lockHandle = await _singletonManager.TryLockAsync(TestLockId, TestInstanceId, attribute, cancellationToken);
            var innerHandle = lockHandle.GetInnerHandle();

            Assert.Equal(TestLockId, innerHandle.LockId);
            // Shouldn't be able to lease this blob
            await Assert.ThrowsAnyAsync<Exception>(async () => await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(15)));

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

                await _testContainerClient.CreateIfNotExistsAsync();
                if (!_testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).Exists())
                {
                    await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).UploadTextAsync("");
                }

                // Lease this blob to test polling behavior
                var lease = await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(15));

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

            await _testContainerClient.CreateIfNotExistsAsync();
            if (!_testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).Exists())
            {
                await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).UploadTextAsync("");
            }

            // Lease this blob to test polling behavior
            var lease = await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(15));

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
                await _testContainerClient.GetBlobClient(string.Format("locks/{0}", TestLockId)).UploadTextAsync("");
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

        private class TestFixture : IDisposable
        {
            public static BlobServiceClient BlobServiceClient;

            public TestFixture()
            {
                // Create a default host since we know that's where the account
                // is coming from
                IHost host = new HostBuilder()
                    .ConfigureDefaultTestHost(b =>
                    {
                        b.AddAzureStorageCoreServices();

                        // Necessary to manipulate Blobs for these tests
                        b.Services.AddSingleton<BlobServiceClientProvider>();
                    })
                    .Build();

                var configuration = host.Services.GetRequiredService<IConfiguration>();
                var blobServiceClientProvider = host.Services.GetRequiredService<BlobServiceClientProvider>();
                BlobServiceClient = blobServiceClientProvider.Get(ConnectionStringNames.Storage, configuration);
            }

            public static BlobContainerClient GetTestContainerClient(string name)
            {
                return BlobServiceClient?.GetBlobContainerClient(name);
            }

            public void Dispose()
            {

                CleanBlobsAsync().Wait();
            }

            private async Task CleanBlobsAsync()
            {
                if (BlobServiceClient != null)
                {
                    await foreach (var testBlobContainer in BlobServiceClient.GetBlobContainersAsync(prefix: TestArtifactContainerPrefix))
                    {
                        try
                        {
                            await BlobServiceClient.DeleteBlobContainerAsync(testBlobContainer.Name);
                        }
                        catch (RequestFailedException)
                        {
                            // best effort
                        }
                    }
                }
            }
        }
    }
}
