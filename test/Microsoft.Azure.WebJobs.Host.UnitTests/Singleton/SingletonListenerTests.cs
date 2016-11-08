// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.WindowsAzure.Storage;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Singleton
{
    public class SingletonListenerTests
    {
        private readonly string TestHostId = "testhostid";
        private readonly SingletonConfiguration _config;
        private readonly Mock<SingletonManager> _mockSingletonManager;
        private readonly Mock<IListener> _mockInnerListener;
        private readonly SingletonListener _listener;
        private readonly SingletonAttribute _attribute;
        private readonly string _lockId;
        private readonly MethodInfo _methodInfo;

        public SingletonListenerTests()
        {
            _methodInfo = this.GetType().GetMethod("TestJob", BindingFlags.Static | BindingFlags.NonPublic);
            _attribute = new SingletonAttribute();
            _config = new SingletonConfiguration
            {
                LockPeriod = TimeSpan.FromSeconds(20)
            };
            _mockSingletonManager = new Mock<SingletonManager>(MockBehavior.Strict, null, null, null, null, new FixedHostIdProvider(TestHostId), null);
            _mockSingletonManager.SetupGet(p => p.Config).Returns(_config);
            _mockInnerListener = new Mock<IListener>(MockBehavior.Strict);
            _listener = new SingletonListener(_methodInfo, _attribute, _mockSingletonManager.Object, _mockInnerListener.Object, new TestTraceWriter(TraceLevel.Info));
            _lockId = SingletonManager.FormatLockId(_methodInfo, SingletonScope.Function, TestHostId, _attribute.ScopeId) + ".Listener";
        }

        [Fact]
        public async Task StartAsync_StartsListener_WhenLockAcquired()
        {
            CancellationToken cancellationToken = new CancellationToken();
            SingletonLockHandle lockHandle = new SingletonLockHandle();
            _mockSingletonManager.Setup(p => p.TryLockAsync(_lockId, null, _attribute, null, _listener.OnLeaseConflictAsync, cancellationToken, false))
                .ReturnsAsync(lockHandle);
            _mockInnerListener.Setup(p => p.StartAsync(cancellationToken)).Returns(Task.FromResult(true));

            await _listener.StartAsync(cancellationToken);

            Assert.Null(_listener.LockTimer);

            _mockSingletonManager.VerifyAll();
            _mockInnerListener.VerifyAll();
        }

        [Fact]
        public async Task StartAsync_DoesNotStartListener_WhenLockCannotBeAcquired()
        {
            CancellationToken cancellationToken = new CancellationToken();
            _mockSingletonManager.Setup(p => p.TryLockAsync(_lockId, null, _attribute, null, _listener.OnLeaseConflictAsync, cancellationToken, false))
                .ReturnsAsync(null);

            await _listener.StartAsync(cancellationToken);

            // verify that the LockTimer has been started
            Assert.NotNull(_listener.LockTimer);
            Assert.True(_listener.LockTimer.AutoReset);
            Assert.True(_listener.LockTimer.Enabled);
            Assert.Equal(_config.ListenerLockRecoveryPollingInterval.TotalMilliseconds, _listener.LockTimer.Interval);

            _mockSingletonManager.VerifyAll();
        }

        [Fact]
        public async Task StartAsync_DoesNotStartLockTimer_WhenPollingIntervalSetToInfinite()
        {
            // we expect the "retry" parameter passed to TryLockAync to be "true"
            // when recovery polling is turned off
            _config.ListenerLockRecoveryPollingInterval = TimeSpan.MaxValue;

            CancellationToken cancellationToken = new CancellationToken();
            _mockSingletonManager.Setup(p => p.TryLockAsync(_lockId, null, _attribute, null, _listener.OnLeaseConflictAsync, cancellationToken, true))
                .ReturnsAsync(null);

            await _listener.StartAsync(cancellationToken);

            // verify that the LockTimer has NOT been started
            Assert.Null(_listener.LockTimer);

            _mockSingletonManager.VerifyAll();
        }

        [Fact]
        public async Task TryAcquireLock_WhenLockAcquired_StopsLockTimerAndStartsListener()
        {
            _listener.LockTimer = new System.Timers.Timer
            {
                Interval = 30 * 1000
            };
            _listener.LockTimer.Start();

            SingletonLockHandle lockHandle = new SingletonLockHandle();
            _mockSingletonManager.Setup(p => p.TryLockAsync(_lockId, null, _attribute, null, _listener.OnLeaseConflictAsync, CancellationToken.None, false))
                .ReturnsAsync(lockHandle);

            _mockInnerListener.Setup(p => p.StartAsync(CancellationToken.None)).Returns(Task.FromResult(true));

            await _listener.TryAcquireLock();

            Assert.Null(_listener.LockTimer);
        }

        [Fact]
        public async Task TryAcquireLock_LockNotAcquired_DoesNotStopLockTimer()
        {
            _listener.LockTimer = new System.Timers.Timer
            {
                Interval = 30 * 1000
            };
            _listener.LockTimer.Start();

            _mockSingletonManager.Setup(p => p.TryLockAsync(_lockId, null, _attribute, null, _listener.OnLeaseConflictAsync, CancellationToken.None, false))
                .ReturnsAsync(null);

            Assert.True(_listener.LockTimer.Enabled);

            await _listener.TryAcquireLock();

            Assert.True(_listener.LockTimer.Enabled);
        }

        [Fact]
        public async Task StopAsync_WhenNotStarted_Noops()
        {
            CancellationToken cancellationToken = new CancellationToken();
            await _listener.StopAsync(cancellationToken);
        }

        [Fact]
        public async Task StopAsync_WhenLockNotAcquired_StopsLockTimer()
        {
            CancellationToken cancellationToken = new CancellationToken();
            _mockSingletonManager.Setup(p => p.TryLockAsync(_lockId, null, _attribute, null, _listener.OnLeaseConflictAsync, cancellationToken, false))
                .ReturnsAsync(null);

            await _listener.StartAsync(cancellationToken);

            Assert.True(_listener.LockTimer.Enabled);

            await _listener.StopAsync(cancellationToken);

            Assert.False(_listener.LockTimer.Enabled);

            _mockSingletonManager.VerifyAll();
            _mockInnerListener.VerifyAll();
        }

        [Fact]
        public async Task StopAsync_WhenLockAcquired_ReleasesLock_AndStopsListener()
        {
            CancellationToken cancellationToken = new CancellationToken();
            SingletonLockHandle lockHandle = new SingletonLockHandle();
            _mockSingletonManager.Setup(p => p.TryLockAsync(_lockId, null, _attribute, null, _listener.OnLeaseConflictAsync, cancellationToken, false))
                .ReturnsAsync(lockHandle);
            _mockInnerListener.Setup(p => p.StartAsync(cancellationToken)).Returns(Task.FromResult(true));

            await _listener.StartAsync(cancellationToken);

            _mockSingletonManager.Setup(p => p.ReleaseLockAsync(lockHandle, cancellationToken)).Returns(Task.FromResult(true));
            _mockInnerListener.Setup(p => p.StopAsync(cancellationToken)).Returns(Task.FromResult(true));

            await _listener.StopAsync(cancellationToken);

            _mockSingletonManager.VerifyAll();
            _mockInnerListener.VerifyAll();
        }

        [Fact]
        public void Cancel_CancelsListener()
        {
            _mockInnerListener.Setup(p => p.Cancel());
            _listener.Cancel();
        }

        [Fact]
        public void Cancel_StopsLockTimer()
        {
            _listener.LockTimer = new System.Timers.Timer
            {
                Interval = 30 * 1000
            };
            _listener.LockTimer.Start();

            _mockInnerListener.Setup(p => p.Cancel());
            _listener.Cancel();

            Assert.False(_listener.LockTimer.Enabled);
        }

        [Fact]
        public void Dispose_DisposesListener()
        {
            _mockInnerListener.Setup(p => p.Dispose());
            _listener.Dispose();
        }

        [Fact]
        public async Task Dispose_WhenLockAcquired_ReleasesLock()
        {
            CancellationToken cancellationToken = new CancellationToken();
            SingletonLockHandle lockHandle = new SingletonLockHandle();
            _mockSingletonManager.Setup(p => p.TryLockAsync(_lockId, null, _attribute, null, _listener.OnLeaseConflictAsync, cancellationToken, false))
                .ReturnsAsync(lockHandle);
            _mockInnerListener.Setup(p => p.StartAsync(cancellationToken)).Returns(Task.FromResult(true));

            await _listener.StartAsync(cancellationToken);

            _mockInnerListener.Setup(p => p.Dispose());
            _mockSingletonManager.Setup(p => p.ReleaseLockAsync(lockHandle, cancellationToken)).Returns(Task.FromResult(true));

            _listener.Dispose();

            _mockSingletonManager.VerifyAll();
            _mockInnerListener.VerifyAll();
        }

        [Fact]
        public void Dispose_DisposesLockTimer()
        {
            _listener.LockTimer = new System.Timers.Timer
            {
                Interval = 30 * 1000
            };
            _listener.LockTimer.Start();

            _mockInnerListener.Setup(p => p.Dispose());
            _listener.Dispose();

            Assert.False(_listener.LockTimer.Enabled);
        }

        [Fact]
        public async Task AcquireLock_LeaseConflict_RestartsInnerListener()
        {
            AutoResetEvent restartedEvent = new AutoResetEvent(false);
            int startCalled = 0;
            _mockInnerListener
                .Setup(p => p.StartAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(0))
                .Callback(() =>
                {
                    // when StartAsync() is called the second time, we know we
                    // have restarted.
                    if (++startCalled == 2)
                    {
                        restartedEvent.Set();
                    }
                });
            _mockInnerListener
                .Setup(p => p.StopAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(0));

            string testLeaseId = "testleaseid";
            IDictionary<string, string> mockBlobMetadata = new Dictionary<string, string>();
            CancellationToken cancellationToken = new CancellationToken();

            SingletonConfiguration singletonConfig = new SingletonConfiguration();
            TestHelpers.SetField(singletonConfig, "_lockPeriod", TimeSpan.FromMilliseconds(500));

            Mock<IStorageBlockBlob> mockStorageBlob = new Mock<IStorageBlockBlob>(MockBehavior.Strict);
            mockStorageBlob
                .Setup(p => p.AcquireLeaseAsync(singletonConfig.LockPeriod, null, cancellationToken))
                .ReturnsAsync(testLeaseId);
            mockStorageBlob
                .Setup(p => p.ReleaseLeaseAsync(It.Is<AccessCondition>(q => q.LeaseId == testLeaseId), null, null, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(0));

            StorageException exception = StorageExceptionFactory.Create(409, "LeaseIdMismatchWithLeaseOperation");
            mockStorageBlob
                .Setup(p => p.RenewLeaseAsync(It.Is<AccessCondition>(q => q.LeaseId == testLeaseId), null, null, cancellationToken))
                .Throws(exception);

            IStorageAccountProvider accountProvider = CreateMockAccountProvider("testhostid/Microsoft.Azure.WebJobs.Host.UnitTests.Singleton.SingletonListenerTests.TestJob.Listener", mockStorageBlob.Object);

            var trace = new TestTraceWriter(TraceLevel.Verbose);

            SingletonManager manager = new SingletonManager(accountProvider, null, singletonConfig, trace, new FixedHostIdProvider("testhostid"));
            manager.MinimumLeaseRenewalInterval = TimeSpan.FromMilliseconds(250);
            SingletonListener listener = new SingletonListener(_methodInfo, _attribute, manager, _mockInnerListener.Object, new TestTraceWriter(TraceLevel.Info));

            await listener.StartAsync(cancellationToken);

            Assert.True(restartedEvent.WaitOne(3000));
            Assert.Null(_listener.LockTimer);
            Assert.Equal(2, startCalled);
            mockStorageBlob.VerifyAll();
            _mockSingletonManager.VerifyAll();
            _mockInnerListener.VerifyAll();
        }

        internal static IStorageAccountProvider CreateMockAccountProvider(string blobName, IStorageBlockBlob blob)
        {
            Mock<IStorageAccountProvider> mockAccountProvider = new Mock<IStorageAccountProvider>(MockBehavior.Strict);
            Mock<IStorageAccount> mockStorageAccount = new Mock<IStorageAccount>(MockBehavior.Strict);
            Mock<IStorageBlobDirectory> mockBlobDirectory = new Mock<IStorageBlobDirectory>(MockBehavior.Strict);
            Mock<IStorageBlobClient> mockBlobClient = new Mock<IStorageBlobClient>(MockBehavior.Strict);
            Mock<IStorageBlobContainer> mockBlobContainer = new Mock<IStorageBlobContainer>(MockBehavior.Strict);

            mockBlobDirectory.Setup(p => p.GetBlockBlobReference(blobName)).Returns(blob);
            mockBlobContainer.Setup(p => p.GetDirectoryReference(HostDirectoryNames.SingletonLocks)).Returns(mockBlobDirectory.Object);
            mockBlobClient.Setup(p => p.GetContainerReference(HostContainerNames.Hosts)).Returns(mockBlobContainer.Object);
            mockStorageAccount.Setup(p => p.CreateBlobClient(null)).Returns(mockBlobClient.Object);
            mockAccountProvider.Setup(p => p.GetAccountAsync(ConnectionStringNames.Storage, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockStorageAccount.Object);

            return mockAccountProvider.Object;
        }

        private static void TestJob()
        {
        }
    }
}