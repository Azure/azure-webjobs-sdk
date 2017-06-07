// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.WebJobs.Host
{
    internal class DefaultSingletonManager : ISingletonManager
    {
        internal const string FunctionInstanceMetadataKey = "FunctionInstance";

        private readonly IStorageAccountProvider _accountProvider;
        private readonly ConcurrentDictionary<string, IStorageBlobDirectory> _lockDirectoryMap = new ConcurrentDictionary<string, IStorageBlobDirectory>(StringComparer.OrdinalIgnoreCase);
                
        private readonly TraceWriter _trace;
        private readonly ILogger _logger;

        private readonly IWebJobsExceptionHandler _exceptionHandler;
        
        private TimeSpan _minimumLeaseRenewalInterval = TimeSpan.FromSeconds(1);

        public DefaultSingletonManager(
            IStorageAccountProvider accountProvider,
            IWebJobsExceptionHandler exceptionHandler, 
            TraceWriter trace,
            ILogger logger)
        {
            _accountProvider = accountProvider;
            _exceptionHandler = exceptionHandler;
            _trace = trace;
            _logger = logger;
        }

        // for testing
        internal TimeSpan MinimumLeaseRenewalInterval
        {
            get
            {
                return _minimumLeaseRenewalInterval;
            }
            set
            {
                _minimumLeaseRenewalInterval = value;
            }
        }

        public async Task ReleaseLockAsync(ISingletonLock lockHandle, CancellationToken cancellationToken)
        {
            SingletonLockHandle singletonLockHandle = (SingletonLockHandle)lockHandle;

            if (singletonLockHandle.LeaseRenewalTimer != null)
            {
                await singletonLockHandle.LeaseRenewalTimer.StopAsync(cancellationToken);
            }

            await ReleaseLeaseAsync(singletonLockHandle.Blob, singletonLockHandle.LeaseId, cancellationToken);
        }

        public async virtual Task<string> GetLockOwnerAsync(string account, string lockId, CancellationToken cancellationToken)
        {
            IStorageBlobDirectory lockDirectory = GetLockDirectory(account);
            IStorageBlockBlob lockBlob = lockDirectory.GetBlockBlobReference(lockId);

            await ReadLeaseBlobMetadata(lockBlob, cancellationToken);

            // if the lease is Available, then there is no current owner
            // (any existing owner value is the last owner that held the lease)
            if (lockBlob.Properties.LeaseState == LeaseState.Available &&
                lockBlob.Properties.LeaseStatus == LeaseStatus.Unlocked)
            {
                return null;
            }

            string owner = string.Empty;
            lockBlob.Metadata.TryGetValue(FunctionInstanceMetadataKey, out owner);

            return owner;
        }

        public async Task<ISingletonLock> TryLockAsync(
            string account,
            string lockId,
            string lockOwnerId,
            TimeSpan lockPeriod,
            CancellationToken cancellationToken)
        {
            IStorageBlobDirectory lockDirectory = GetLockDirectory(account);
            IStorageBlockBlob lockBlob = lockDirectory.GetBlockBlobReference(lockId);
            string leaseId = await TryAcquireLeaseAsync(lockBlob, lockPeriod, cancellationToken);

            if (string.IsNullOrEmpty(leaseId))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(lockOwnerId))
            {
                await WriteLeaseBlobMetadata(lockBlob, leaseId, lockOwnerId, cancellationToken);
            }

            SingletonLockHandle lockHandle = new SingletonLockHandle
            {
                LeaseId = leaseId,
                LockId = lockId,
                Blob = lockBlob,
                LeaseRenewalTimer = CreateLeaseRenewalTimer(lockBlob, leaseId, lockId, lockPeriod, _exceptionHandler)
            };

            // start the renewal timer, which ensures that we maintain our lease until
            // the lock is released
            lockHandle.LeaseRenewalTimer.Start();

            return lockHandle;
        }

        internal IStorageBlobDirectory GetLockDirectory(string accountName)
        {
            if (string.IsNullOrEmpty(accountName))
            {
                accountName = ConnectionStringNames.Storage;
            }

            IStorageBlobDirectory storageDirectory = null;
            if (!_lockDirectoryMap.TryGetValue(accountName, out storageDirectory))
            {
                Task<IStorageAccount> task = _accountProvider.GetStorageAccountAsync(accountName, CancellationToken.None);
                IStorageAccount storageAccount = task.Result;
                // singleton requires block blobs, cannot be premium
                storageAccount.AssertTypeOneOf(StorageAccountType.GeneralPurpose, StorageAccountType.BlobOnly);
                IStorageBlobClient blobClient = storageAccount.CreateBlobClient();
                storageDirectory = blobClient.GetContainerReference(HostContainerNames.Hosts)
                                       .GetDirectoryReference(HostDirectoryNames.SingletonLocks);
                _lockDirectoryMap[accountName] = storageDirectory;
            }

            return storageDirectory;
        }

        private static async Task<string> TryAcquireLeaseAsync(IStorageBlockBlob blob, TimeSpan leasePeriod, CancellationToken cancellationToken)
        {
            bool blobDoesNotExist = false;
            try
            {
                // Optimistically try to acquire the lease. The blob may not yet
                // exist. If it doesn't we handle the 404, create it, and retry below
                return await blob.AcquireLeaseAsync(leasePeriod, null, cancellationToken);
            }
            catch (StorageException exception)
            {
                if (exception.RequestInformation != null)
                {
                    if (exception.RequestInformation.HttpStatusCode == 409)
                    {
                        return null;
                    }
                    else if (exception.RequestInformation.HttpStatusCode == 404)
                    {
                        blobDoesNotExist = true;
                    }
                    else
                    {
                        throw;
                    }
                }
                else
                {
                    throw;
                }
            }

            if (blobDoesNotExist)
            {
                await TryCreateAsync(blob, cancellationToken);

                try
                {
                    return await blob.AcquireLeaseAsync(leasePeriod, null, cancellationToken);
                }
                catch (StorageException exception)
                {
                    if (exception.RequestInformation != null &&
                        exception.RequestInformation.HttpStatusCode == 409)
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

        private static async Task ReleaseLeaseAsync(IStorageBlockBlob blob, string leaseId, CancellationToken cancellationToken)
        {
            try
            {
                // Note that this call returns without throwing if the lease is expired. See the table at:
                // http://msdn.microsoft.com/en-us/library/azure/ee691972.aspx
                await blob.ReleaseLeaseAsync(
                    accessCondition: new AccessCondition { LeaseId = leaseId },
                    options: null,
                    operationContext: null,
                    cancellationToken: cancellationToken);
            }
            catch (StorageException exception)
            {
                if (exception.RequestInformation != null)
                {
                    if (exception.RequestInformation.HttpStatusCode == 404 ||
                        exception.RequestInformation.HttpStatusCode == 409)
                    {
                        // if the blob no longer exists, or there is another lease
                        // now active, there is nothing for us to release so we can
                        // ignore
                    }
                    else
                    {
                        throw;
                    }
                }
                else
                {
                    throw;
                }
            }
        }

        private ITaskSeriesTimer CreateLeaseRenewalTimer(IStorageBlockBlob leaseBlob, string leaseId, string lockId, TimeSpan leasePeriod,
            IWebJobsExceptionHandler exceptionHandler)
        {
            // renew the lease when it is halfway to expiring   
            TimeSpan normalUpdateInterval = new TimeSpan(leasePeriod.Ticks / 2);

            IDelayStrategy speedupStrategy = new LinearSpeedupStrategy(normalUpdateInterval, MinimumLeaseRenewalInterval);
            ITaskSeriesCommand command = new RenewLeaseCommand(leaseBlob, leaseId, lockId, speedupStrategy, _trace, _logger, leasePeriod);
            return new TaskSeriesTimer(command, exceptionHandler, Task.Delay(normalUpdateInterval));
        }

        private static async Task<bool> TryCreateAsync(IStorageBlockBlob blob, CancellationToken cancellationToken)
        {
            bool isContainerNotFoundException = false;

            try
            {
                await blob.UploadTextAsync(string.Empty, cancellationToken: cancellationToken);
                return true;
            }
            catch (StorageException exception)
            {
                if (exception.RequestInformation != null)
                {
                    if (exception.RequestInformation.HttpStatusCode == 404)
                    {
                        isContainerNotFoundException = true;
                    }
                    else if (exception.RequestInformation.HttpStatusCode == 409 ||
                             exception.RequestInformation.HttpStatusCode == 412)
                    {
                        // The blob already exists, or is leased by someone else
                        return false;
                    }
                    else
                    {
                        throw;
                    }
                }
                else
                {
                    throw;
                }
            }

            Debug.Assert(isContainerNotFoundException);
            await blob.Container.CreateIfNotExistsAsync(cancellationToken);

            try
            {
                await blob.UploadTextAsync(string.Empty, cancellationToken: cancellationToken);
                return true;
            }
            catch (StorageException exception)
            {
                if (exception.RequestInformation != null &&
                    (exception.RequestInformation.HttpStatusCode == 409 || exception.RequestInformation.HttpStatusCode == 412))
                {
                    // The blob already exists, or is leased by someone else
                    return false;
                }
                else
                {
                    throw;
                }
            }
        }

        private static async Task WriteLeaseBlobMetadata(IStorageBlockBlob blob, string leaseId, string functionInstanceId, CancellationToken cancellationToken)
        {
            blob.Metadata.Add(FunctionInstanceMetadataKey, functionInstanceId);

            await blob.SetMetadataAsync(
                accessCondition: new AccessCondition { LeaseId = leaseId },
                options: null,
                operationContext: null,
                cancellationToken: cancellationToken);
        }

        private static async Task ReadLeaseBlobMetadata(IStorageBlockBlob blob, CancellationToken cancellationToken)
        {
            try
            {
                await blob.FetchAttributesAsync(cancellationToken);
            }
            catch (StorageException exception)
            {
                if (exception.RequestInformation != null &&
                    exception.RequestInformation.HttpStatusCode == 404)
                {
                    // the blob no longer exists
                }
                else
                {
                    throw;
                }
            }
        }

        internal class SingletonLockHandle : ISingletonLock
        {
            private readonly TaskCompletionSource<bool> _lockLost = new TaskCompletionSource<bool>();

            public string LeaseId { get; set; }
            public string LockId { get; set; }
            public IStorageBlockBlob Blob { get; set; }
            public ITaskSeriesTimer LeaseRenewalTimer { get; set; }

            public Task LeaseLost => _lockLost.Task;                        
        }

        internal class RenewLeaseCommand : ITaskSeriesCommand
        {
            private readonly IStorageBlockBlob _leaseBlob;
            private readonly string _leaseId;
            private readonly string _lockId;
            private readonly IDelayStrategy _speedupStrategy;
            private readonly TraceWriter _trace;
            private readonly ILogger _logger;
            private DateTimeOffset _lastRenewal;
            private TimeSpan _lastRenewalLatency;
            private TimeSpan _leasePeriod;

            public RenewLeaseCommand(IStorageBlockBlob leaseBlob, string leaseId, string lockId, IDelayStrategy speedupStrategy, TraceWriter trace,
                ILogger logger, TimeSpan leasePeriod)
            {
                _lastRenewal = DateTimeOffset.UtcNow;
                _leaseBlob = leaseBlob;
                _leaseId = leaseId;
                _lockId = lockId;
                _speedupStrategy = speedupStrategy;
                _trace = trace;
                _logger = logger;
                _leasePeriod = leasePeriod;
            }

            public async Task<TaskSeriesCommandResult> ExecuteAsync(CancellationToken cancellationToken)
            {
                TimeSpan delay;

                try
                {
                    AccessCondition condition = new AccessCondition
                    {
                        LeaseId = _leaseId
                    };
                    DateTimeOffset requestStart = DateTimeOffset.UtcNow;
                    await _leaseBlob.RenewLeaseAsync(condition, null, null, cancellationToken);
                    _lastRenewal = DateTime.UtcNow;
                    _lastRenewalLatency = _lastRenewal - requestStart;

                    // The next execution should occur after a normal delay.
                    delay = _speedupStrategy.GetNextDelay(executionSucceeded: true);
                }
                catch (StorageException exception)
                {
                    if (exception.IsServerSideError())
                    {
                        // The next execution should occur more quickly (try to renew the lease before it expires).
                        delay = _speedupStrategy.GetNextDelay(executionSucceeded: false);
                        string msg = string.Format(CultureInfo.InvariantCulture, "Singleton lock renewal failed for blob '{0}' with error code {1}. Retry renewal in {2} milliseconds.",
                            _lockId, FormatErrorCode(exception), delay.TotalMilliseconds);
                        _trace.Warning(msg, source: TraceSource.Execution);
                        _logger?.LogWarning(msg);
                    }
                    else
                    {
                        // Log the details we've been accumulating to help with debugging this scenario
                        int leasePeriodMilliseconds = (int)_leasePeriod.TotalMilliseconds;
                        string lastRenewalFormatted = _lastRenewal.ToString("yyyy-MM-ddTHH:mm:ss.FFFZ", CultureInfo.InvariantCulture);
                        int millisecondsSinceLastSuccess = (int)(DateTime.UtcNow - _lastRenewal).TotalMilliseconds;
                        int lastRenewalMilliseconds = (int)_lastRenewalLatency.TotalMilliseconds;

                        string msg = string.Format(CultureInfo.InvariantCulture, "Singleton lock renewal failed for blob '{0}' with error code {1}. The last successful renewal completed at {2} ({3} milliseconds ago) with a duration of {4} milliseconds. The lease period was {5} milliseconds.",
                            _lockId, FormatErrorCode(exception), lastRenewalFormatted, millisecondsSinceLastSuccess, lastRenewalMilliseconds, leasePeriodMilliseconds);
                        _trace.Error(msg);
                        _logger?.LogError(msg);

                        // If we've lost the lease or cannot re-establish it, we want to fail any
                        // in progress function execution
                        throw;
                    }
                }
                return new TaskSeriesCommandResult(wait: Task.Delay(delay));
            }

            private static string FormatErrorCode(StorageException exception)
            {
                int statusCode;
                if (!exception.TryGetStatusCode(out statusCode))
                {
                    return "''";
                }

                string message = statusCode.ToString(CultureInfo.InvariantCulture);

                string errorCode = exception.GetErrorCode();

                if (errorCode != null)
                {
                    message += ": " + errorCode;
                }

                return message;
            }
        }
    }
}