﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Singleton
{
    public class SingletonManagerTests
    {
        private const string TestLockId = "testid";
        private const string TestInstanceId = "testinstance";
        private const string TestLeaseId = "testleaseid";

        private SingletonManager _singletonManager;
        private SingletonConfiguration _singletonConfig;
        private Mock<IStorageBlobDirectory> _mockBlobDirectory;
        private Mock<IBackgroundExceptionDispatcher> _mockExceptionDispatcher;
        private Mock<IStorageBlockBlob> _mockStorageBlob;
        private TestTraceWriter _trace = new TestTraceWriter(TraceLevel.Verbose);
        private Dictionary<string, string> _mockBlobMetadata;
        private TestNameResolver _nameResolver;

        public SingletonManagerTests()
        {
            _mockBlobDirectory = new Mock<IStorageBlobDirectory>(MockBehavior.Strict);
            Mock<IStorageBlobClient> mockBlobClient = new Mock<IStorageBlobClient>(MockBehavior.Strict);
            Mock<IStorageBlobContainer> mockBlobContainer = new Mock<IStorageBlobContainer>(MockBehavior.Strict);
            mockBlobContainer.Setup(p => p.GetDirectoryReference(HostDirectoryNames.SingletonLocks)).Returns(_mockBlobDirectory.Object);
            mockBlobClient.Setup(p => p.GetContainerReference(HostContainerNames.Hosts)).Returns(mockBlobContainer.Object);
            _mockExceptionDispatcher = new Mock<IBackgroundExceptionDispatcher>(MockBehavior.Strict);

            _mockStorageBlob = new Mock<IStorageBlockBlob>(MockBehavior.Strict);
            _mockBlobMetadata = new Dictionary<string, string>();
            _mockBlobDirectory.Setup(p => p.GetBlockBlobReference(TestLockId)).Returns(_mockStorageBlob.Object);

            _singletonConfig = new SingletonConfiguration();

            // use reflection to bypass the normal validations (so tests can run fast)
            TestHelpers.SetField(_singletonConfig, "_lockAcquisitionPollingInterval", TimeSpan.FromMilliseconds(25));
            TestHelpers.SetField(_singletonConfig, "_lockPeriod", TimeSpan.FromMilliseconds(500));
            _singletonConfig.LockAcquisitionTimeout = TimeSpan.FromMilliseconds(200);

            _nameResolver = new TestNameResolver(); 
            _singletonManager = new SingletonManager(mockBlobClient.Object, _mockExceptionDispatcher.Object, _singletonConfig, _trace, _nameResolver);

            _singletonManager.MinimumLeaseRenewalInterval = TimeSpan.FromMilliseconds(250);
        }

        [Fact]
        public async Task TryLockAsync_CreatesBlobLease_WithAutoRenewal()
        {
            CancellationToken cancellationToken = new CancellationToken();
            _mockStorageBlob.SetupGet(p => p.Metadata).Returns(_mockBlobMetadata);
            _mockStorageBlob.Setup(p => p.UploadTextAsync(string.Empty, null, It.Is<AccessCondition>(q => q.IfNoneMatchETag == "*"), null, null, cancellationToken)).Returns(Task.FromResult(true));
            _mockStorageBlob.Setup(p => p.AcquireLeaseAsync(_singletonConfig.LockPeriod, null, cancellationToken)).ReturnsAsync(TestLeaseId);
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
            SingletonManager.SingletonLockHandle lockHandle = (SingletonManager.SingletonLockHandle)await _singletonManager.TryLockAsync(TestLockId, TestInstanceId, attribute, cancellationToken);

            Assert.Same(_mockStorageBlob.Object, lockHandle.Blob);
            Assert.Equal(TestLeaseId, lockHandle.LeaseId);
            Assert.Equal(1, _mockStorageBlob.Object.Metadata.Keys.Count);
            Assert.Equal(_mockStorageBlob.Object.Metadata[SingletonManager.FunctionInstanceMetadataKey], TestInstanceId);

            // wait for enough time that we expect some lease renewals to occur
            int duration = 2000;
            int expectedRenewalCount = (int)(duration / (_singletonConfig.LockPeriod.TotalMilliseconds / 2)) - 1;
            await Task.Delay(duration);

            Assert.Equal(expectedRenewalCount, renewCount);

            // now release the lock and verify no more renewals
            await _singletonManager.ReleaseLockAsync(lockHandle, cancellationToken);

            // verify the traces
            Assert.Equal(1, _trace.Traces.Count(p => p.ToString().Contains("Verbose Waiting for Singleton lock (testid)")));
            Assert.Equal(1, _trace.Traces.Count(p => p.ToString().Contains("Verbose Singleton lock acquired (testid)")));
            Assert.Equal(renewCount, _trace.Traces.Count(p => p.ToString().Contains("Renewing Singleton lock (testid)")));
            Assert.Equal(1, _trace.Traces.Count(p => p.ToString().Contains("Verbose Singleton lock released (testid)")));

            renewCount = 0;
            await Task.Delay(1000);

            Assert.Equal(0, renewCount);

            _mockStorageBlob.VerifyAll();
        }

        [Fact]
        public async Task TryLockAsync_WithContention_PollsForLease()
        {
            CancellationToken cancellationToken = new CancellationToken();
            _mockStorageBlob.SetupGet(p => p.Metadata).Returns(_mockBlobMetadata);
            _mockStorageBlob.Setup(p => p.UploadTextAsync(string.Empty, null, It.Is<AccessCondition>(q => q.IfNoneMatchETag == "*"), null, null, cancellationToken)).Returns(Task.FromResult(true));
            _mockStorageBlob.Setup(p => p.SetMetadataAsync(It.Is<AccessCondition>(q => q.LeaseId == TestLeaseId), null, null, cancellationToken)).Returns(Task.FromResult(true));

            int numRetries = 3;
            int count = 0;
            _mockStorageBlob.Setup(p => p.AcquireLeaseAsync(_singletonConfig.LockPeriod, null, cancellationToken))
                .Returns(() =>
            {
                count++;
                return Task.FromResult<string>(count > numRetries ? TestLeaseId : null);
            });

            SingletonAttribute attribute = new SingletonAttribute();
            SingletonManager.SingletonLockHandle lockHandle = (SingletonManager.SingletonLockHandle)await _singletonManager.TryLockAsync(TestLockId, TestInstanceId, attribute, cancellationToken);

            Assert.Equal(TestLeaseId, lockHandle.LeaseId);
            Assert.Equal(numRetries, count - 1);
            Assert.NotNull(lockHandle.LeaseRenewalTimer);

            _mockStorageBlob.VerifyAll();
        }

        [Fact]
        public async Task LockAsync_WithContention_AcquisitionTimeoutExpires_Throws()
        {
            CancellationToken cancellationToken = new CancellationToken();
            _mockStorageBlob.Setup(p => p.UploadTextAsync(string.Empty, null, It.Is<AccessCondition>(q => q.IfNoneMatchETag == "*"), null, null, cancellationToken)).Returns(Task.FromResult(true));

            int count = 0;
            _mockStorageBlob.Setup(p => p.AcquireLeaseAsync(_singletonConfig.LockPeriod, null, cancellationToken))
                .Callback<TimeSpan?, string, CancellationToken>((mockPeriod, mockLeaseId, mockCancellationToken) =>
                {
                    ++count;
                }).Returns(() =>
                {
                    return Task.FromResult<string>(null);
                });

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

            SingletonManager.SingletonLockHandle handle = new SingletonManager.SingletonLockHandle
            {
                Blob = _mockStorageBlob.Object,
                LeaseId = TestLeaseId,
                LeaseRenewalTimer = mockRenewalTimer.Object
            };

            await _singletonManager.ReleaseLockAsync(handle, cancellationToken);

            mockRenewalTimer.VerifyAll();
        }

        [Fact]
        public async Task GetLockOwnerAsync_LeaseLocked_ReturnsOwner()
        {
            CancellationToken cancellationToken = new CancellationToken();
            _mockStorageBlob.SetupGet(p => p.Metadata).Returns(_mockBlobMetadata);
            _mockStorageBlob.Setup(p => p.FetchAttributesAsync(cancellationToken)).Returns(Task.FromResult(true));

            Mock<IStorageBlobProperties> mockBlobProperties = new Mock<IStorageBlobProperties>(MockBehavior.Strict);
            mockBlobProperties.Setup(p => p.LeaseState).Returns(LeaseState.Leased);
            _mockStorageBlob.SetupGet(p => p.Properties).Returns(mockBlobProperties.Object);

            string lockOwner = await _singletonManager.GetLockOwnerAsync(TestLockId, CancellationToken.None);
            Assert.Equal(null, lockOwner);

            _mockBlobMetadata.Add(SingletonManager.FunctionInstanceMetadataKey, TestLockId);
            lockOwner = await _singletonManager.GetLockOwnerAsync(TestLockId, CancellationToken.None);
            Assert.Equal(TestLockId, lockOwner);

            mockBlobProperties.VerifyAll();
            _mockStorageBlob.VerifyAll();
        }

        [Fact]
        public async Task GetLockOwnerAsync_LeaseAvailable_ReturnsNull()
        {
            CancellationToken cancellationToken = new CancellationToken();
            _mockStorageBlob.Setup(p => p.FetchAttributesAsync(cancellationToken)).Returns(Task.FromResult(true));

            Mock<IStorageBlobProperties> mockBlobProperties = new Mock<IStorageBlobProperties>(MockBehavior.Strict);
            mockBlobProperties.Setup(p => p.LeaseState).Returns(LeaseState.Available);
            mockBlobProperties.Setup(p => p.LeaseStatus).Returns(LeaseStatus.Unlocked);
            _mockStorageBlob.SetupGet(p => p.Properties).Returns(mockBlobProperties.Object);

            string lockOwner = await _singletonManager.GetLockOwnerAsync(TestLockId, CancellationToken.None);
            Assert.Equal(null, lockOwner);

            mockBlobProperties.VerifyAll();
            _mockStorageBlob.VerifyAll();
        }

        [Theory]
        [InlineData(null, "Microsoft.Azure.WebJobs.Host.UnitTests.Singleton.SingletonManagerTests.TestJob")]
        [InlineData("", "Microsoft.Azure.WebJobs.Host.UnitTests.Singleton.SingletonManagerTests.TestJob")]
        [InlineData("testscope", "Microsoft.Azure.WebJobs.Host.UnitTests.Singleton.SingletonManagerTests.TestJob.testscope")]
        public void FormatLockId_ReturnsExpectedValue(string scope, string expectedLockId)
        {
            MethodInfo methodInfo = this.GetType().GetMethod("TestJob", BindingFlags.Static | BindingFlags.NonPublic);
            string actualLockId = SingletonManager.FormatLockId(methodInfo, scope);
            Assert.Equal(expectedLockId, actualLockId);
        }

        [Fact]
        public void GetBoundScope_Success_ReturnsExceptedResult()
        {
            Dictionary<string, object> bindingData = new Dictionary<string, object>();
            bindingData.Add("Region", "testregion");
            bindingData.Add("Zone", 1);

            string result = _singletonManager.GetBoundScope(@"{Region}\{Zone}", bindingData);

            Assert.Equal(@"testregion\1", result);
        }

        [Fact]
        public void GetBoundScopeAppSettings_Success_ReturnsExceptedResult()
        {
            var bindingData = AppSettingsBinding.CreateBindingData();

            string result = SingletonManager.GetBoundScope(@"{testkey}", bindingData);

            Assert.Equal(@"testval", result);
        }

        [Fact]
        public void GetBoundScope_BindingError_Throws()
        {
            // Missing binding data for "Zone"
            Dictionary<string, object> bindingData = new Dictionary<string, object>();
            bindingData.Add("Region", "testregion");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _singletonManager.GetBoundScope(@"{Region}\{Zone}", bindingData));

            Assert.Equal("No value for named parameter 'Zone'.", exception.Message);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("scope", "scope")]
        public void GetBoundScope_NullBindingDataScenarios_Succeeds(string scope, string expectedResult)
        {
            string result = _singletonManager.GetBoundScope(scope, null);
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("scope", "scope")]
        [InlineData("scope{P1}", "scopeTest1")]
        [InlineData("scope:{P1}-{P2}", "scope:Test1-Test2")]
        [InlineData("%var1%", "Value1")]
        [InlineData("{P1}%var2%{P2}%var1%", "Test1Value2Test2Value1")]
        public void GetBoundScope_BindingDataScenarios_Succeeds(string scope, string expectedResult)
        {
            Dictionary<string, object> bindingData = new Dictionary<string, object>();
            bindingData.Add("P1", "Test1");
            bindingData.Add("P2", "Test2");

            _nameResolver.Names.Add("var1", "Value1");
            _nameResolver.Names.Add("var2", "Value2");

            string result = _singletonManager.GetBoundScope(scope, bindingData);
            Assert.Equal(expectedResult, result);
        }

        private static void TestJob()
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
                string value = null;
                if (Names.TryGetValue(name, out value))
                {
                    return value;
                }
                throw new NotSupportedException(string.Format("Cannot resolve name: '{0}'", name));
            }
        }
    }
}
