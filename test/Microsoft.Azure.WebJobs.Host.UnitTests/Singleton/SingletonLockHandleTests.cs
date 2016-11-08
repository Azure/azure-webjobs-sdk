// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.WindowsAzure.Storage;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Singleton
{
    public class SingletonLockHandleTests
    {
        private readonly string TestLeaseId = "testleaseid";
        private readonly string TestLockId = "testlockid";
        private IDelayStrategy _speedupStrategy;
        private ISingletonRenewalMonitor _renewalMonitor;
        private Mock<IStorageBlockBlob> _mockBlob;
        private StorageException _conflictException;
        private readonly TraceWriter _trace;
        private int _renewLeaseCalls = 0;
        private int _renewalMonitorCalls = 0;
        private int _conflictCalls = 0;

        public SingletonLockHandleTests()
        {
            _speedupStrategy = new LinearSpeedupStrategy(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(250));
            _mockBlob = new Mock<IStorageBlockBlob>(MockBehavior.Strict);
            _conflictException = StorageExceptionFactory.Create(409, "LeaseIdMismatchWithLeaseOperation");
            _trace = new TestTraceWriter(TraceLevel.Verbose);
            var mockRenewalMonitor = new Mock<ISingletonRenewalMonitor>(MockBehavior.Strict);
            mockRenewalMonitor
                .Setup(p => p.OnRenewal(It.IsAny<DateTime>(), 500))
                .Callback<DateTime, double>((date, interval) =>
                {
                    _renewalMonitorCalls++;
                });
            _renewalMonitor = mockRenewalMonitor.Object;
        }

        [Fact]
        public async Task StartTimer_RenewsLease_OnSchedule()
        {
            var handle = CreateSingletonLockHandle(OnLeaseConflict, _renewalMonitor);

            _mockBlob
                .Setup(p => p.RenewLeaseAsync(It.Is<AccessCondition>(a => a.LeaseId == TestLeaseId), null, null, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(0))
                .Callback(() => _renewLeaseCalls++);

            handle.StartTimer();

            int seconds = 3;
            int expectedCalls = seconds * 2 - 1;
            await Task.Delay(TimeSpan.FromSeconds(seconds));

            Assert.Equal(0, _conflictCalls);
            Assert.Equal(expectedCalls, _renewLeaseCalls);
            Assert.Equal(expectedCalls, _renewalMonitorCalls);
        }

        [Fact]
        public async Task RenewalTimerElapsedAsync_RenewsLeaseAndUpdatesState()
        {
            var handle = CreateSingletonLockHandle(OnLeaseConflict, _renewalMonitor);

            _mockBlob
                .Setup(p => p.RenewLeaseAsync(It.Is<AccessCondition>(a => a.LeaseId == TestLeaseId), null, null, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(0))
                .Callback(() => _renewLeaseCalls++);

            var nextInterval = await handle.RenewalTimerElapsedAsync(CancellationToken.None);

            Assert.Equal(500, nextInterval);
            Assert.Equal(1, _renewalMonitorCalls);
            Assert.Equal(0, _conflictCalls);
            Assert.Equal(1, _renewLeaseCalls);
            Assert.InRange(handle.LastRenewalTime, DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow);
            Assert.Equal(500, handle.LastRenewalInterval);
        }

        [Fact]
        public async Task RenewalTimerElapsedAsync_WithNullCallbackAndMontior_RenewsLeaseAndUpdatesState()
        {
            var handle = CreateSingletonLockHandle(null, null);

            _mockBlob
                .Setup(p => p.RenewLeaseAsync(It.Is<AccessCondition>(a => a.LeaseId == TestLeaseId), null, null, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(0))
                .Callback(() => _renewLeaseCalls++);

            var nextInterval = await handle.RenewalTimerElapsedAsync(CancellationToken.None);

            Assert.Equal(500, nextInterval);
            Assert.Equal(0, _renewalMonitorCalls);
            Assert.Equal(0, _conflictCalls);
            Assert.Equal(1, _renewLeaseCalls);
            Assert.InRange(handle.LastRenewalTime, DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow);
            Assert.Equal(500, handle.LastRenewalInterval);
        }

        private Task OnLeaseConflict()
        {
            _conflictCalls++;
            return Task.FromResult(0);
        }

        private SingletonLockHandle CreateSingletonLockHandle(Func<Task> onLeaseConflict, ISingletonRenewalMonitor renewalMonitor)
        {
            return new SingletonLockHandle(_mockBlob.Object, TestLeaseId, TestLockId, _speedupStrategy, onLeaseConflict, renewalMonitor, _trace);
        }
    }
}