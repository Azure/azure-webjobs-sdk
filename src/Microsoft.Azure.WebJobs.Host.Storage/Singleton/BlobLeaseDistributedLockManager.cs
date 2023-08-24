// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Provides a BlobClient lease-based implementation of the <see cref="IDistributedLockManager"/> service for singleton locking.
    /// </summary>
    internal class BlobLeaseDistributedLockManager : IDistributedLockManager
    {
        internal const string FunctionInstanceMetadataKey = "FunctionInstance";
        internal const string SingletonLocks = "locks";

        private readonly ILogger _logger;
        private readonly IAzureBlobStorageProvider _blobStorageProvider;
        private readonly ConcurrentDictionary<string, BlobContainerClient> _lockBlobContainerClientMap = new ConcurrentDictionary<string, BlobContainerClient>(StringComparer.OrdinalIgnoreCase);

        public BlobLeaseDistributedLockManager(
            ILoggerFactory loggerFactory,
            IAzureBlobStorageProvider azureStorageProvider) // Take an ILoggerFactory since that's a DI component.
        {
            _logger = loggerFactory.CreateLogger(LogCategories.Singleton);
            _blobStorageProvider = azureStorageProvider;
        }

        public Task<bool> RenewAsync(IDistributedLock lockHandle, CancellationToken cancellationToken)
        {
            SingletonLockHandle singletonLockHandle = (SingletonLockHandle)lockHandle;
            return singletonLockHandle.RenewAsync(_logger, cancellationToken);
        }

        public async Task ReleaseLockAsync(IDistributedLock lockHandle, CancellationToken cancellationToken)
        {
            SingletonLockHandle singletonLockHandle = (SingletonLockHandle)lockHandle;
            await ReleaseLeaseAsync(singletonLockHandle.BlobLeaseClient, singletonLockHandle.LeaseId, cancellationToken);
        }

        public async virtual Task<string> GetLockOwnerAsync(string account, string lockId, CancellationToken cancellationToken)
        {
            var lockBlob = this.GetContainerClient(account).GetBlobClient(GetLockPath(lockId));

            var blobProperties = await ReadLeaseBlobMetadata(lockBlob, cancellationToken);

            // if the lease is Available, then there is no current owner
            // (any existing owner value is the last owner that held the lease)
            if (blobProperties != null &&
                blobProperties.LeaseState == LeaseState.Available &&
                blobProperties.LeaseStatus == LeaseStatus.Unlocked)
            {
                return null;
            }

            string owner = default;
            blobProperties?.Metadata.TryGetValue(FunctionInstanceMetadataKey, out owner);
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
            var lockBlob = this.GetContainerClient(account).GetBlobClient(GetLockPath(lockId));
            string leaseId = await TryAcquireLeaseAsync(lockBlob, lockPeriod, proposedLeaseId, cancellationToken);

            if (string.IsNullOrEmpty(leaseId))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(lockOwnerId))
            {
                await WriteLeaseBlobMetadata(lockBlob, leaseId, lockOwnerId, cancellationToken);
            }

            SingletonLockHandle lockHandle = new SingletonLockHandle(leaseId, lockId, this.GetBlobLeaseClient(lockBlob, leaseId), lockPeriod);

            return lockHandle;
        }

        protected virtual BlobContainerClient GetContainerClient(string connectionName)
        {
            if (string.IsNullOrEmpty(connectionName))
            {
                // Dictionary lookup needs non-null string
                connectionName = string.Empty;
            }

            // First check the cache if we have a BlobContainerClient for this connection
            if (_lockBlobContainerClientMap.TryGetValue(connectionName, out BlobContainerClient blobContainerClient))
            {
                return blobContainerClient;
            }

            BlobContainerClient containerClient = CreateBlobContainerClient(connectionName);
            _lockBlobContainerClientMap[connectionName] = containerClient;
            return containerClient;
        }

        // Helper method to retrieve a new BlobContainerClient with ability to override the default storage account
        private BlobContainerClient CreateBlobContainerClient(string connectionName)
        {
            if (!string.IsNullOrEmpty(connectionName))
            {
                if (_blobStorageProvider.TryCreateBlobServiceClientFromConnection(connectionName, out BlobServiceClient client))
                {
                    return client.GetBlobContainerClient(HostContainerNames.Hosts);
                }
                else
                {
                    throw new InvalidOperationException($"Could not create BlobContainerClient for {typeof(BlobLeaseDistributedLockManager).Name} with Connection: {connectionName}.");
                }
            }
            else
            {
                if (!_blobStorageProvider.TryCreateHostingBlobContainerClient(out BlobContainerClient blobContainerClient))
                {
                    throw new InvalidOperationException($"Could not create BlobContainerClient for {typeof(BlobLeaseDistributedLockManager).Name}.");
                }

                return blobContainerClient;
            }
        }

        internal string GetLockPath(string lockId)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}/{1}", SingletonLocks, lockId);
        }

        // Allows the extension method to be mocked for testing
        protected virtual BlobLeaseClient GetBlobLeaseClient(BlobClient blobClient, string proposedLeaseId)
        {
            return blobClient.GetBlobLeaseClient(proposedLeaseId);
        }

        // Allows the extension method to be mocked for testing
        protected virtual BlobContainerClient GetParentBlobContainerClient(BlobClient blobClient)
        {
            return blobClient.GetParentBlobContainerClient();
        }

        private async Task<string> TryAcquireLeaseAsync(
            BlobClient blobClient,
            TimeSpan leasePeriod,
            string proposedLeaseId,
            CancellationToken cancellationToken)
        {
            bool blobDoesNotExist = false;
            try
            {
                // Check if a lease is available before trying to acquire. The blob may not
                // yet exist; if it doesn't we handle the 404, create it, and retry below.
                // The reason we're checking to see if the lease is available before trying
                // to acquire is to avoid the flood of 409 errors that Application Insights
                // picks up when a lease cannot be acquired due to conflict; see issue #2318.
                var blobProperties = await ReadLeaseBlobMetadata(blobClient, cancellationToken);

                switch (blobProperties?.LeaseState)
                {
                    case null:
                    case LeaseState.Available:
                    case LeaseState.Expired:
                    case LeaseState.Broken:
                        var leaseResponse = await GetBlobLeaseClient(blobClient, proposedLeaseId).AcquireAsync(leasePeriod, cancellationToken: cancellationToken);
                        return leaseResponse.Value.LeaseId;
                    default:
                        return null;
                }
            }
            catch (RequestFailedException exception)
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
                await TryCreateAsync(blobClient, cancellationToken);

                try
                {
                    var leaseResponse = await GetBlobLeaseClient(blobClient, proposedLeaseId).AcquireAsync(leasePeriod, cancellationToken: cancellationToken);
                    return leaseResponse.Value.LeaseId;
                }
                catch (RequestFailedException exception)
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

        private static async Task ReleaseLeaseAsync(BlobLeaseClient blobLeaseClient, string leaseId, CancellationToken cancellationToken)
        {
            try
            {
                // Note that this call returns without throwing if the lease is expired. See the table at:
                // http://msdn.microsoft.com/en-us/library/azure/ee691972.aspx
                await blobLeaseClient.ReleaseAsync(cancellationToken: cancellationToken);
            }
            catch (RequestFailedException exception)
            {
                if (exception.Status == 404 ||
                    exception.Status == 409)
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
        }

        private async Task<bool> TryCreateAsync(BlobClient blobClient, CancellationToken cancellationToken)
        {
            bool isContainerNotFoundException = false;

            try
            {
                using (Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(string.Empty)))
                {
                    await blobClient.UploadAsync(stream, cancellationToken: cancellationToken);
                }
                return true;
            }
            catch (RequestFailedException exception)
            {
                if (exception.Status == 404)
                {
                    isContainerNotFoundException = true;
                }
                else if (exception.Status == 409 ||
                            exception.Status == 412)
                {
                    // The blob already exists, or is leased by someone else
                    return false;
                }
                else
                {
                    throw;
                }
            }

            Debug.Assert(isContainerNotFoundException);

            var container = GetParentBlobContainerClient(blobClient);
            try
            {
                await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            }
            catch (RequestFailedException exception)
            when (exception.Status == 409 && string.Compare("ContainerBeingDeleted", exception.ErrorCode) == 0)
            {
                throw new RequestFailedException("The host container is pending deletion and currently inaccessible.");
            }

            try
            {
                using (Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(string.Empty)))
                {
                    await blobClient.UploadAsync(stream, cancellationToken: cancellationToken);
                }

                return true;
            }
            catch (RequestFailedException exception)
            {
                if (exception.Status == 409 || exception.Status == 412)
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

        private static async Task WriteLeaseBlobMetadata(BlobClient blobClient, string leaseId, string functionInstanceId, CancellationToken cancellationToken)
        {
            var blobProperties = await ReadLeaseBlobMetadata(blobClient, cancellationToken);
            if (blobProperties != null)
            {
                blobProperties.Metadata[FunctionInstanceMetadataKey] = functionInstanceId;
                await blobClient.SetMetadataAsync(
                    blobProperties.Metadata,
                    new BlobRequestConditions { LeaseId = leaseId },
                    cancellationToken: cancellationToken);
            }
        }

        private static async Task<BlobProperties> ReadLeaseBlobMetadata(BlobClient blobClient, CancellationToken cancellationToken)
        {
            try
            {
                var propertiesResponse = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
                return propertiesResponse.Value;
            }
            catch (RequestFailedException exception)
            {
                if (exception.Status == 404)
                {
                    // the blob no longer exists
                    return null;
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

            public SingletonLockHandle(string leaseId, string lockId, BlobLeaseClient blobLeaseClient, TimeSpan leasePeriod)
            {
                this.LeaseId = leaseId;
                this.LockId = lockId;
                this._leasePeriod = leasePeriod;
                this.BlobLeaseClient = blobLeaseClient;
            }

            public string LeaseId { get; internal set; }

            public string LockId { get; internal set; }

            public BlobLeaseClient BlobLeaseClient { get; internal set; }

            public async Task<bool> RenewAsync(ILogger logger, CancellationToken cancellationToken)
            {
                try
                {
                    DateTimeOffset requestStart = DateTimeOffset.UtcNow;
                    await this.BlobLeaseClient.RenewAsync(cancellationToken: cancellationToken);
                    _lastRenewal = DateTime.UtcNow;
                    _lastRenewalLatency = _lastRenewal - requestStart;

                    // The next execution should occur after a normal delay.
                    return true;
                }
                catch (RequestFailedException exception)
                {
                    // indicates server-side error
                    if (exception.Status >= 500 && exception.Status < 600)
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

            private static string FormatErrorCode(RequestFailedException exception)
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
