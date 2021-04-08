// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Provides a BlobClient lease-based implementation of the <see cref="IDistributedLockManager"/> service for singleton locking.
    /// Specify the ILeaseProviderFactory service to control how locking is implemented.
    /// The default behavior uses <see cref="AzureBlobLeaseProvider"/> which is container based. 
    /// Hosts can provide a derived implementation to leverage the accountName and allow different hosts to share.
    /// </summary>
    public class GenericDistributedLockManager : IDistributedLockManager
    {
        internal const string FunctionInstanceMetadataKey = "FunctionInstance";

        // Convention for container name to use.
        public const string DefaultContainerName = HostContainerNames.Hosts;

        private readonly ILogger _logger;
        private readonly ILeaseProviderFactory _leaseProviderFactory;

        public GenericDistributedLockManager(ILoggerFactory loggerFactory, ILeaseProviderFactory leaseProviderFactory)
        {
            _logger = loggerFactory.CreateLogger(LogCategories.Singleton);
            _leaseProviderFactory = leaseProviderFactory;
        }

        public Task<bool> RenewAsync(IDistributedLock lockHandle, CancellationToken cancellationToken)
        {
            SingletonLockHandle singletonLockHandle = (SingletonLockHandle)lockHandle;
            return singletonLockHandle.RenewAsync(_logger, cancellationToken);
        }

        public async Task ReleaseLockAsync(IDistributedLock lockHandle, CancellationToken cancellationToken)
        {
            SingletonLockHandle singletonLockHandle = (SingletonLockHandle)lockHandle;
            await ReleaseLeaseAsync(singletonLockHandle.LeaseProvider, singletonLockHandle.LeaseId, cancellationToken);
        }

        public async virtual Task<string> GetLockOwnerAsync(string account, string lockId, CancellationToken cancellationToken)
        {
            var owner = await _leaseProviderFactory.GetLeaseProvider(lockId).GetLeaseOwner(cancellationToken);
            return owner;            
        }

        public async Task<IDistributedLock> TryLockAsync(
            string account,
            string lockId,
            string lockOwnerId,
            string proposedLeaseId,
            TimeSpan lockPeriod,
            CancellationToken cancellationToken)
        {
            var leaseProvider = this._leaseProviderFactory.GetLeaseProvider(lockId, account);
            string leaseId = await TryAcquireLeaseAsync(leaseProvider, lockPeriod, proposedLeaseId, cancellationToken);

            if (string.IsNullOrEmpty(leaseId))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(lockOwnerId))
            {
                await leaseProvider.SetLeaseOwner(lockOwnerId, leaseId, cancellationToken);
            }

            SingletonLockHandle lockHandle = new SingletonLockHandle(leaseId, leaseProvider, lockPeriod);

            return lockHandle;
        }

        internal string GetLockPath(string lockId)
        {
            // lockId here is already in the format {accountName}/{functionDescriptor}.{scopeId}
            return string.Format(CultureInfo.InvariantCulture, "{0}/{1}", HostDirectoryNames.SingletonLocks, lockId);
        }

        private static async Task<string> TryAcquireLeaseAsync(
            ILeaseProvider leaseProvider,
            TimeSpan leasePeriod,
            string proposedLeaseId,
            CancellationToken cancellationToken)
        {
            bool blobDoesNotExist = false;
            try
            {
                // Optimistically try to acquire the lease. The blob may not yet
                // exist. If it doesn't we handle the 404, create it, and retry below
                var leaseId = await leaseProvider.AcquireAsync(leasePeriod, cancellationToken, leaseId: proposedLeaseId);
                return leaseId;
            }
            catch (LeaseException exception)
            {

                if (exception.Status == 409)
                {
                    return null;
                }
                else if (exception.Status == 404)
                {
                    blobDoesNotExist = true;
                }
                else
                {
                    throw;
                }
            }

            if (blobDoesNotExist)
            {
                await TryCreateAsync(leaseProvider, cancellationToken);

                try
                {

                    var leaseId = await leaseProvider.AcquireAsync(leasePeriod, cancellationToken, leaseId: proposedLeaseId);
                    return leaseId;
                }
                catch (LeaseException exception)
                {
                    if (exception.Status == 409)
                    {
                        return null;
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return null;
        }

        private static async Task ReleaseLeaseAsync(ILeaseProvider leaseProvider, string leaseId, CancellationToken cancellationToken)
        {
            try
            {
                // Note that this call returns without throwing if the lease is expired. See the table at:
                // http://msdn.microsoft.com/en-us/library/azure/ee691972.aspx
                await leaseProvider.ReleaseAsync(leaseId, cancellationToken);
            }
            catch (LeaseException ex) when (ex is LeaseNotObtainedException || ex is LeaseConflictException)
            {
                
                // if the blob no longer exists, or there is another lease
                // now active, there is nothing for us to release so we can
                // ignore
            }
            catch (Exception)
            {
                throw;
            }
        }

        private static async Task<bool> TryCreateAsync(ILeaseProvider leaseProvider, CancellationToken cancellationToken)
        {
            try
            {
                await leaseProvider.CreateLeaseAsync(cancellationToken);
                return true;
            }
            catch (LeaseException exception)
            {
                if (exception is LeaseNotCreatedException)
                {
                    return false;
                }
                else
                {
                    throw;
                }
            }
        }

        internal class SingletonLockHandle : IDistributedLock
        {
            private readonly TimeSpan _leasePeriod;

            private DateTimeOffset _lastRenewal;
            private TimeSpan _lastRenewalLatency;


            public SingletonLockHandle()
            {
            }

            public SingletonLockHandle(string leaseId, ILeaseProvider leaseProvider, TimeSpan leasePeriod)
            {
                this.LeaseId = leaseId;
                this._leasePeriod = leasePeriod;
                this.LeaseProvider = leaseProvider;
            }

            public string LeaseId { get; internal set; }

            public ILeaseProvider LeaseProvider { get; internal set; }

            public string LockId
            {
                get
                {
                    return LeaseProvider.GetLockId();
                }
            }

            public async Task<bool> RenewAsync(ILogger logger, CancellationToken cancellationToken)
            {
                try
                {
                    DateTimeOffset requestStart = DateTimeOffset.UtcNow;
                    await LeaseProvider.RenewAsync(LeaseId, cancellationToken: cancellationToken);
                    _lastRenewal = DateTime.UtcNow;
                    _lastRenewalLatency = _lastRenewal - requestStart;

                    // The next execution should occur after a normal delay.
                    return true;
                }
                catch (LeaseException exception)
                {
                    if (exception.Status >= 500 && exception.Status < 600) // indicates server-side error
                    {
                        string msg = string.Format(CultureInfo.InvariantCulture, "Singleton lock renewal failed for blob '{0}' with error code {1}.",
                            this.LockId, FormatErrorCode(exception));
                        logger?.LogWarning(msg);
                        return false; // The next execution should occur more quickly (try to renew the lease before it expires).
                    }
                    else
                    {
                        // Log the details we've been accumulating to help with debugging this scenario
                        int leasePeriodMilliseconds = (int)_leasePeriod.TotalMilliseconds;
                        string lastRenewalFormatted = _lastRenewal.ToString("yyyy-MM-ddTHH:mm:ss.FFFZ", CultureInfo.InvariantCulture);
                        int millisecondsSinceLastSuccess = (int)(DateTime.UtcNow - _lastRenewal).TotalMilliseconds;
                        int lastRenewalMilliseconds = (int)_lastRenewalLatency.TotalMilliseconds;

                        string msg = string.Format(CultureInfo.InvariantCulture, "Singleton lock renewal failed for blob '{0}' with error code {1}. The last successful renewal completed at {2} ({3} milliseconds ago) with a duration of {4} milliseconds. The lease period was {5} milliseconds.",
                            this.LockId, FormatErrorCode(exception), lastRenewalFormatted, millisecondsSinceLastSuccess, lastRenewalMilliseconds, leasePeriodMilliseconds);
                        logger?.LogError(msg);

                        // If we've lost the lease or cannot re-establish it, we want to fail any
                        // in progress function execution
                        throw;
                    }
                }
            }

            private static string FormatErrorCode(LeaseException exception)
            {
                string message = exception.Status.ToString(CultureInfo.InvariantCulture);

                string errorCode = exception.ErrorCode;

                if (errorCode != null)
                {
                    message += ": " + errorCode;
                }

                return message;
            }
        }
    }
}
