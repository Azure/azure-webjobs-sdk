// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests
{
    [Trait(TestTraits.CategoryTraitName, TestTraits.DynamicConcurrency)]
    public class PrimaryHostCoordinatorTests
    {
        private TestLoggerProvider _loggerProvider = new TestLoggerProvider();
        private bool _enabled;

        public PrimaryHostCoordinatorTests()
        {
            _enabled = true;
        }

        private IHost CreateHost(Action<IServiceCollection> configure = null)
        {
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<Program>(b =>
                {
                    b.AddAzureStorageCoreServices();
                })
                .ConfigureServices(s =>
                {
                    s.AddOptions<PrimaryHostCoordinatorOptions>().Configure(o =>
                    {
                        o.Enabled = _enabled;
                    });

                    configure?.Invoke(s);
                })
                .ConfigureLogging(b =>
                {
                    b.AddProvider(_loggerProvider);
                })
                .Build();

            return host;
        }

        [Theory]
        [InlineData(14.99)]
        [InlineData(60.01)]
        public void RejectsInvalidLeaseTimeout(double leaseTimeoutSeconds)
        {
            var leaseTimeout = TimeSpan.FromSeconds(leaseTimeoutSeconds);
            Assert.Throws<ArgumentOutOfRangeException>(() => new PrimaryHostCoordinatorOptions { LeaseTimeout = leaseTimeout });
        }

        [Fact]
        public async Task Disabled_DoesNotStartTimer()
        {
            // disable the host coordinator
            _enabled = false;

            var host = CreateHost();
            using (host)
            {
                await host.StartAsync();

                // verify the lease timer isn't running
                var hostedServices = host.Services.GetServices<IHostedService>();
                var coordinator = (PrimaryHostCoordinator)hostedServices.Single(p => p.GetType() == typeof(PrimaryHostCoordinator));
                Assert.False(coordinator.LeaseTimerRunning);

                await host.StopAsync();
            }
        }

        [Fact]
        public async Task HasLease_WhenLeaseIsAcquired_ReturnsTrue()
        {
            var host = CreateHost();

            var containerClient = GetHostingBlobContainerClient(host);
            string hostId = GetHostId(host);

            using (host)
            {
                await host.StartAsync();

                var primaryState = host.Services.GetService<IPrimaryHostStateProvider>();
                await TestHelpers.Await(() => primaryState.IsPrimary);

                await host.StopAsync();
            }

            await ClearLeaseBlob(containerClient, hostId);
        }

        [Fact]
        public async Task HasLeaseChanged_WhenLeaseIsAcquired()
        {
            var host = CreateHost();

            var containerClient = GetHostingBlobContainerClient(host);
            string hostId = GetHostId(host);

            BlobClient blobClient = await GetLockBlobAsync(containerClient, hostId);

            // Acquire a lease on the host lock blob
            string leaseId = (await blobClient.GetBlobLeaseClient().AcquireAsync(TimeSpan.FromMinutes(1))).Value.LeaseId;

            using (host)
            {
                await host.StartAsync();

                var primaryState = host.Services.GetService<IPrimaryHostStateProvider>();

                // The test owns the lease, so the host doesn't have it.
                Assert.False(primaryState.IsPrimary);

                // Now release it, and we should reclaim it.
                await blobClient.GetBlobLeaseClient(leaseId).ReleaseAsync();

                await TestHelpers.Await(() => primaryState.IsPrimary,
                    userMessageCallback: () => $"{nameof(IPrimaryHostStateProvider.IsPrimary)} was not correctly set to 'true' when lease was acquired.");

                await host.StopAsync();
            }

            await ClearLeaseBlob(containerClient, hostId);
        }

        [Fact]
        public async Task HasLeaseChanged_WhenLeaseIsLost()
        {
            var host = CreateHost();

            var containerClient = GetHostingBlobContainerClient(host);
            string hostId = GetHostId(host);

            using (host)
            {
                BlobClient blobClient = await GetLockBlobAsync(containerClient, hostId);
                var primaryState = host.Services.GetService<IPrimaryHostStateProvider>();
                var manager = host.Services.GetServices<IHostedService>().OfType<PrimaryHostCoordinator>().Single();
                var lockManager = host.Services.GetService<IDistributedLockManager>();
                string tempLeaseId = null;

                await host.StartAsync();

                try
                {
                    await TestHelpers.Await(() => primaryState.IsPrimary);

                    // Release the manager's lease and acquire one with a different id
                    await lockManager.ReleaseLockAsync(manager.LockHandle, CancellationToken.None);
                    tempLeaseId = (await blobClient.GetBlobLeaseClient(Guid.NewGuid().ToString()).AcquireAsync(TimeSpan.FromSeconds(30))).Value.LeaseId;

                    await TestHelpers.Await(() => !primaryState.IsPrimary,
                        userMessageCallback: () => $"{nameof(IPrimaryHostStateProvider.IsPrimary)} was not correctly set to 'false' when lease lost.");
                }
                finally
                {
                    if (tempLeaseId != null)
                    {
                        await blobClient.GetBlobLeaseClient(tempLeaseId).ReleaseAsync();
                    }
                }

                await host.StopAsync();
            }

            await ClearLeaseBlob(containerClient, hostId);
        }

        [Fact]
        public async Task Dispose_ReleasesBlobLease()
        {
            var host = CreateHost();

            var containerClient = GetHostingBlobContainerClient(host);
            string hostId = GetHostId(host);
            var primaryHostCoordinator = host.Services.GetServices<IHostedService>().OfType<PrimaryHostCoordinator>().Single();

            using (host)
            {
                await host.StartAsync();

                var primaryState = host.Services.GetService<IPrimaryHostStateProvider>();
                await TestHelpers.Await(() => primaryState.IsPrimary);

                await host.StopAsync();
            }

            // Container disposal is a fire-and-forget so this service disposal could be delayed. This will force it.
            primaryHostCoordinator.Dispose();

            BlobClient blobClient = await GetLockBlobAsync(containerClient, hostId);

            string leaseId = null;
            try
            {
                // Acquire a lease on the host lock blob
                leaseId = (await blobClient.GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(15))).Value.LeaseId;

                await blobClient.GetBlobLeaseClient(leaseId).ReleaseAsync();
            }
            catch (RequestFailedException exc) when (exc.Status == 409)
            {
            }

            Assert.False(string.IsNullOrEmpty(leaseId), "Failed to acquire a blob lease. The lease was not properly released.");

            await ClearLeaseBlob(containerClient, hostId);
        }

        [Fact]
        public async Task TraceOutputsMessagesWhenLeaseIsAcquired()
        {
            var blobMock = new Mock<IDistributedLockManager>();
            blobMock.Setup(b => b.TryLockAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<IDistributedLock>(new FakeLock()));

            var host = CreateHost(s =>
            {
                s.AddSingleton<IDistributedLockManager>(_ => blobMock.Object);
            });

            string hostId = GetHostId(host);
            string instanceId = Microsoft.Azure.WebJobs.Utility.GetInstanceId();

            using (host)
            {
                await host.StartAsync();

                // Make sure we have enough time to trace the renewal
                await TestHelpers.Await(() => _loggerProvider.GetAllLogMessages().Any(m => m.FormattedMessage.StartsWith("Host lock lease acquired by instance ID ")), 10000, 500);

                LogMessage acquisitionEvent = _loggerProvider.GetAllLogMessages().Last();
                Assert.Contains($"Host lock lease acquired by instance ID '{instanceId}'.", acquisitionEvent.FormattedMessage);
                Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, acquisitionEvent.Level);

                await host.StopAsync();
            }
        }

        [Fact]
        public async Task TraceOutputsMessagesWhenLeaseRenewalFails()
        {
            var renewResetEvent = new ManualResetEventSlim();

            var blobMock = new Mock<IDistributedLockManager>();
            blobMock.Setup(b => b.TryLockAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<IDistributedLock>(new FakeLock()));

            blobMock.Setup(b => b.RenewAsync(It.IsAny<IDistributedLock>(), It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromException<bool>(new RequestFailedException(409, "test")))
                .Callback(() => renewResetEvent.Set());

            var host = CreateHost(s =>
            {
                s.AddSingleton<IDistributedLockManager>(_ => blobMock.Object);
            });

            string hostId = GetHostId(host);
            string instanceId = Microsoft.Azure.WebJobs.Utility.GetInstanceId();

            using (host)
            {
                await host.StartAsync();

                renewResetEvent.Wait(TimeSpan.FromSeconds(10));
                await TestHelpers.Await(() => _loggerProvider.GetAllLogMessages().Any(m => m.FormattedMessage.StartsWith("Failed to renew host lock lease: ")), 10000, 500);

                await host.StopAsync();
            }

            LogMessage acquisitionEvent = _loggerProvider.GetAllLogMessages().Single(m => m.FormattedMessage.Contains($"Host lock lease acquired by instance ID '{instanceId}'."));
            Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, acquisitionEvent.Level);

            LogMessage renewalEvent = _loggerProvider.GetAllLogMessages().Single(m => m.FormattedMessage.Contains(@"Failed to renew host lock lease: Another host has acquired the lease."));
            string pattern = @"Failed to renew host lock lease: Another host has acquired the lease. The last successful renewal completed at (.+) \([0-9]+ milliseconds ago\) with a duration of [0-9]+ milliseconds.";
            Assert.True(Regex.IsMatch(renewalEvent.FormattedMessage, pattern), $"Expected trace event {pattern} not found.");
            Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, renewalEvent.Level);
        }

        [Fact]
        public async Task DifferentHosts_UsingSameStorageAccount_CanObtainLease()
        {
            string hostId1 = Guid.NewGuid().ToString();
            string hostId2 = Guid.NewGuid().ToString();

            var host1 = CreateHost(s =>
            {
                s.AddSingleton<IHostIdProvider>(_ => new FixedHostIdProvider(hostId1));
            });

            Assert.True(host1.Services.GetRequiredService<IAzureBlobStorageProvider>().TryCreateHostingBlobContainerClient(out BlobContainerClient containerClient1));

            var host2 = CreateHost(s =>
            {
                s.AddSingleton<IHostIdProvider>(_ => new FixedHostIdProvider(hostId2));
            });
            Assert.True(host2.Services.GetRequiredService<IAzureBlobStorageProvider>().TryCreateHostingBlobContainerClient(out BlobContainerClient containerClient2));

            using (host1)
            using (host2)
            {
                await host1.StartAsync();
                await host2.StartAsync();

                var primaryState1 = host1.Services.GetService<IPrimaryHostStateProvider>();
                var primaryState2 = host2.Services.GetService<IPrimaryHostStateProvider>();

                Task manager1Check = TestHelpers.Await(() => primaryState1.IsPrimary);
                Task manager2Check = TestHelpers.Await(() => primaryState2.IsPrimary);

                await Task.WhenAll(manager1Check, manager2Check);

                await host1.StopAsync();
                await host2.StopAsync();
            }

            await Task.WhenAll(ClearLeaseBlob(containerClient1, hostId1), ClearLeaseBlob(containerClient2, hostId2));
        }

        private static async Task<BlobClient> GetLockBlobAsync(BlobContainerClient containerClient, string hostId)
        {
            await containerClient.CreateIfNotExistsAsync();

            // The BlobLeaseDistributedLockManager puts things under the /locks path by default
            var blobClient = containerClient.GetBlobClient("locks/" + PrimaryHostCoordinator.GetBlobName(hostId));
            if (!await blobClient.ExistsAsync())
            {
                await blobClient.UploadTextAsync("", overwrite: true);
            }

            return blobClient;
        }

        private async Task ClearLeaseBlob(BlobContainerClient containerClient, string hostId)
        {
            BlobClient blobClient = await GetLockBlobAsync(containerClient, hostId);

            try
            {
                await blobClient.GetBlobLeaseClient().BreakAsync(TimeSpan.Zero);
            }
            catch
            {
            }

            await blobClient.DeleteIfExistsAsync();
        }

        private class FakeLock : IDistributedLock
        {
            public string LockId => "lockid";

            public Task LeaseLost => throw new NotImplementedException();
        }

        private class FixedHostIdProvider : IHostIdProvider
        {
            private readonly string _hostId;

            public FixedHostIdProvider(string hostId)
            {
                _hostId = hostId;
            }

            public Task<string> GetHostIdAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(_hostId);
            }
        }

        public static string GetHostId(IHost host)
        {
            return host.Services.GetService<IHostIdProvider>().GetHostIdAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        public static BlobContainerClient GetHostingBlobContainerClient(IHost host)
        {
            host.Services.GetRequiredService<IAzureBlobStorageProvider>().TryCreateHostingBlobContainerClient(out BlobContainerClient containerClient);
            return containerClient;
        }

        public class Program
        {
            [NoAutomaticTrigger]
            public void TestFunction()
            {
            }
        }
    }
}
