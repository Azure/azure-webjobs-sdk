// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Lease;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.WebJobs.Host.Lease
{
    // Azure Blob Storage based lease implementation
    internal class BlobLeaseProxy : ILeaseProxy
    {
        private readonly IStorageAccountProvider _storageAccountProvider;
        private readonly ConcurrentDictionary<string, IStorageAccount> _storageAccountMap = new ConcurrentDictionary<string, IStorageAccount>(StringComparer.OrdinalIgnoreCase);

        public BlobLeaseProxy(IStorageAccountProvider storageAccountProvider)
        {
            if (storageAccountProvider == null)
            {
                throw new ArgumentNullException(nameof(storageAccountProvider));
            }

            _storageAccountProvider = storageAccountProvider;
        }

        /// <inheritdoc />
        public async Task<string> TryAcquireLeaseAsync(LeaseDefinition leaseDefinition, CancellationToken cancellationToken)
        {
            try
            {
                return await AcquireLeaseAsync(leaseDefinition, cancellationToken);
            }
            catch
            {
                return null;
            }
        }

        public async Task<string> AcquireLeaseAsync(LeaseDefinition leaseDefinition, CancellationToken cancellationToken)
        {
            IStorageBlockBlob lockBlob = null;
            try
            {
                lockBlob = GetBlob(leaseDefinition);

                // Optimistically try to acquire the lease. The blob may not exist yet.
                // If it doesn't exist, we handle the 404, create it, and retry below.
                return await lockBlob.AcquireLeaseAsync(leaseDefinition.Period, leaseDefinition.LeaseId, cancellationToken);
            }
            catch (StorageException exception)
            {
                if (exception.RequestInformation == null)
                {
                    throw new LeaseException(LeaseFailureReason.Unknown, exception);
                }

                if (exception.RequestInformation.HttpStatusCode == 404)
                {
                    // No action needed. We will create the blob and retry again.
                }
                else if (exception.RequestInformation.HttpStatusCode == 409)
                {
                    throw new LeaseException(LeaseFailureReason.Conflict, exception);
                }
                else
                {
                    throw new LeaseException(LeaseFailureReason.Unknown, exception);
                }
            }

            try
            {
                await TryCreateBlobAsync(lockBlob, cancellationToken);
                return await lockBlob.AcquireLeaseAsync(leaseDefinition.Period, proposedLeaseId: null, cancellationToken: cancellationToken);
            }
            catch (StorageException exception)
            {
                if (exception.RequestInformation != null &&
                    exception.RequestInformation.HttpStatusCode == 409)
                {
                    throw new LeaseException(LeaseFailureReason.Conflict, exception);
                }

                throw new LeaseException(LeaseFailureReason.Unknown, exception);
            }
        }

        /// <inheritdoc />
        public Task RenewLeaseAsync(LeaseDefinition leaseDefinition, CancellationToken cancellationToken)
        {
            if (leaseDefinition == null)
            {
                throw new ArgumentNullException(nameof(leaseDefinition));
            }

            try
            {
                IStorageBlockBlob lockBlob = GetBlob(leaseDefinition);
                var accessCondition = new AccessCondition
                {
                    LeaseId = leaseDefinition.LeaseId
                };

                return lockBlob.RenewLeaseAsync(accessCondition, options: null, operationContext: null, cancellationToken: cancellationToken);
            }
            catch (StorageException exception)
            {
                throw new LeaseException(LeaseFailureReason.Unknown, exception);
            }
        }

        /// <inheritdoc />
        public async Task WriteLeaseMetadataAsync(LeaseDefinition leaseDefinition, string key, string value, CancellationToken cancellationToken)
        {
            try
            {
                IStorageBlockBlob blob = GetBlob(leaseDefinition);
                blob.Metadata.Add(key, value);

                await blob.SetMetadataAsync(
                    accessCondition: new AccessCondition { LeaseId = leaseDefinition.LeaseId },
                    options: null,
                    operationContext: null,
                    cancellationToken: cancellationToken);
            }
            catch (StorageException exception)
            {
                throw new LeaseException(LeaseFailureReason.Unknown, exception);
            }
        }

        /// <inheritdoc />
        public async Task ReleaseLeaseAsync(LeaseDefinition leaseDefinition, CancellationToken cancellationToken)
        {
            try
            {
                IStorageBlockBlob blob = GetBlob(leaseDefinition);
                // Note that this call returns without throwing if the lease is expired. See the table at:
                // http://msdn.microsoft.com/en-us/library/azure/ee691972.aspx
                await blob.ReleaseLeaseAsync(
                    accessCondition: new AccessCondition { LeaseId = leaseDefinition.LeaseId },
                    options: null,
                    operationContext: null,
                    cancellationToken: cancellationToken);
            }
            catch (StorageException exception)
            {
                if (exception.RequestInformation == null)
                {
                    throw new LeaseException(LeaseFailureReason.Unknown, exception);
                }

                if (exception.RequestInformation.HttpStatusCode == 404 ||
                    exception.RequestInformation.HttpStatusCode == 409)
                {
                    // if the blob no longer exists, or there is another lease
                    // now active, there is nothing for us to release so we can
                    // ignore
                }
                else
                {
                    throw new LeaseException(LeaseFailureReason.Unknown, exception);
                }
            }
        }

        /// <inheritdoc />
        public async Task<LeaseInformation> ReadLeaseInfoAsync(LeaseDefinition leaseDefinition, CancellationToken cancellationToken)
        {
            try
            {
                IStorageBlob lockBlob = GetBlob(leaseDefinition);

                await FetchLeaseBlobMetadataAsync(lockBlob, cancellationToken);

                var isLeaseAvailable = lockBlob.Properties.LeaseState == LeaseState.Available &&
                                        lockBlob.Properties.LeaseStatus == LeaseStatus.Unlocked;

                return new LeaseInformation(isLeaseAvailable, lockBlob.Metadata);
            }
            catch (StorageException exception)
            {
                throw new LeaseException(LeaseFailureReason.Unknown, exception);
            }
        }

        private static async Task FetchLeaseBlobMetadataAsync(IStorageBlob blob, CancellationToken cancellationToken)
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

        private static async Task<bool> TryCreateBlobAsync(IStorageBlockBlob blob, CancellationToken cancellationToken)
        {
            bool isContainerNotFoundException = false;

            try
            {
                await blob.UploadTextAsync(string.Empty, cancellationToken: cancellationToken);
                return true;
            }
            catch (StorageException exception)
            {
                if (exception.RequestInformation == null)
                {
                    throw;
                }

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

            Debug.Assert(isContainerNotFoundException);

            // Create the container if it does not exist.
            // Directories need not be created as they are created automatically, if needed.
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

        internal IStorageBlockBlob GetBlob(LeaseDefinition leaseDefinition)
        {
            var accountName = leaseDefinition.AccountName;

            if (string.IsNullOrWhiteSpace(accountName))
            {
                throw new InvalidOperationException("No lease account name specified");
            }

            IStorageAccount storageAccount;
            if (!_storageAccountMap.TryGetValue(accountName, out storageAccount))
            {
                storageAccount = _storageAccountProvider.GetStorageAccountAsync(accountName, CancellationToken.None).Result;
                
                // singleton requires block blobs, cannot be premium
                storageAccount.AssertTypeOneOf(StorageAccountType.GeneralPurpose, StorageAccountType.BlobOnly);

                _storageAccountMap[accountName] = storageAccount;
            }

            string containerName, directoryName;
            GetBlobPathComponents(leaseDefinition, out containerName, out directoryName);

            IStorageBlobClient blobClient = storageAccount.CreateBlobClient();
            IStorageBlobContainer container = blobClient.GetContainerReference(containerName);

            IStorageBlockBlob blob;
            if (string.IsNullOrWhiteSpace(directoryName))
            {
                blob = container.GetBlockBlobReference(leaseDefinition.Name);
            }
            else
            {
                IStorageBlobDirectory blobDirectory = container.GetDirectoryReference(directoryName);
                blob = blobDirectory.GetBlockBlobReference(leaseDefinition.Name);
            }

            return blob;
        }

        // Gets the storage container and directory names from the lease definition
        private static void GetBlobPathComponents(LeaseDefinition leaseDefinition, out string containerName, out string directoryName)
        {
            containerName = directoryName = null;

            if (leaseDefinition.Namespaces == null || leaseDefinition.Namespaces.Count < 1 || leaseDefinition.Namespaces.Count > 2)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Invalid LeaseDefinition Namespaces: {0}", leaseDefinition.Namespaces));
            }

            containerName = leaseDefinition.Namespaces[0];

            if (leaseDefinition.Namespaces.Count == 2)
            {
                directoryName = leaseDefinition.Namespaces[1];
            }
        }
    }
}
