// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Singleton;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host
{
    // This is an implementation of IDistributedLockManager to be used when running
    // in Kubernetes environments.
    public class KubernetesDistributedLockManager : IDistributedLockManager
    {
        private readonly ILogger _logger;
        private readonly KubernetesClient _kubernetesClient;

        public KubernetesDistributedLockManager(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(LogCategories.Singleton);
            _kubernetesClient = new KubernetesClient();
        }

        public async Task<string> GetLockOwnerAsync(string account, string lockId, CancellationToken cancellationToken)
        {
            var response = await _kubernetesClient.GetLock(lockId);
            return response.Owner;
        }

        public async Task ReleaseLockAsync(IDistributedLock lockHandle, CancellationToken cancellationToken)
        {
            var kubernetesLock = (KubernetesLockHandle)lockHandle;
            var response = await _kubernetesClient.ReleaseLock(kubernetesLock.LockId, kubernetesLock.OwnerId);
            response.EnsureSuccessStatusCode();
        }

        public async Task<bool> RenewAsync(IDistributedLock lockHandle, CancellationToken cancellationToken)
        {
            var kubernetesLock = (KubernetesLockHandle)lockHandle;
            await _kubernetesClient.TryAcquireLock(kubernetesLock.LockId, kubernetesLock.OwnerId, kubernetesLock.LockPeriod);
            return true;
        }

        public async Task<IDistributedLock> TryLockAsync(string account, string lockId, string lockOwnerId, string proposedLeaseId, TimeSpan lockPeriod, CancellationToken cancellationToken)
        {
            var kubernetesLock = await _kubernetesClient.TryAcquireLock(lockId, lockOwnerId, lockPeriod.ToString());
            if (string.IsNullOrEmpty(kubernetesLock.LockId))
            {
                return null;
            }
            return kubernetesLock;
        }
    }
}
