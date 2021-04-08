// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
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
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Host
{
    public class AzureBlobLeaseProvider : ILeaseProvider
    {
        private string _lockId;
        private BlobContainerClient _blobContainerClient;

        public AzureBlobLeaseProvider(string lockId, BlobContainerClient blobContainerClient)
        {
            _lockId = lockId;
            _blobContainerClient = blobContainerClient;
        }

        public virtual async Task<string> AcquireAsync(TimeSpan duration, CancellationToken cancellationToken, string leaseId = null)
        {
            try
            {
                var blobClient = _blobContainerClient.GetBlobClient(GetLockPath(_lockId));
                var leaseResponse = await GetBlobLeaseClient(blobClient, leaseId).AcquireAsync(duration, cancellationToken: cancellationToken);
                return leaseResponse.Value.LeaseId;
            }
            catch (RequestFailedException exception)
            {
                if (exception.Status == 409)
                {
                    throw new LeaseConflictException(exception.Message, exception)
                    {
                        Status = exception.Status,
                        ErrorCode = exception.ErrorCode
                    };
                }
                else if (exception.Status == 404)
                {
                    throw new LeaseNotObtainedException(exception.Message, exception)
                    {
                        Status = exception.Status,
                        ErrorCode = exception.ErrorCode
                    };
                }

                throw new LeaseException(exception.Message, exception)
                {
                    Status = exception.Status,
                    ErrorCode = exception.ErrorCode
                };
            }
        }

        public virtual async Task CreateLeaseAsync(CancellationToken cancellationToken)
        {
            bool isContainerNotFoundException = false;

            try
            {
                var blobClient = _blobContainerClient.GetBlobClient(GetLockPath(_lockId));
                using (Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(String.Empty)))
                {
                    await blobClient.UploadAsync(stream, cancellationToken: cancellationToken);
                }

                return;
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
                    throw new LeaseNotCreatedException("Blob already exists or is leased by someone else.", exception)
                    {
                        Status = exception.Status,
                        ErrorCode = exception.ErrorCode
                    };
                }
                else
                {
                    throw new LeaseException("Unable to create lease blob", exception)
                    {
                        Status = exception.Status,
                        ErrorCode = exception.ErrorCode
                    };
                }
            }

            Debug.Assert(isContainerNotFoundException);

            try
            {
                await _blobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            }
            catch (RequestFailedException exception)
            when (exception.Status == 409 && string.Compare("ContainerBeingDeleted", exception.ErrorCode) == 0)
            {
                throw new LeaseException("The host container is pending deletion and currently inaccessible.", exception)
                {
                    Status = exception.Status,
                    ErrorCode = exception.ErrorCode
                };
            }

            try
            {
                var blobClient = _blobContainerClient.GetBlobClient(GetLockPath(_lockId));
                using (Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(String.Empty)))
                {
                    await blobClient.UploadAsync(stream, cancellationToken: cancellationToken);
                }

                return;
            }
            catch (RequestFailedException exception)
            {
                if (exception.Status == 409 || exception.Status == 412)
                {
                    // The blob already exists, or is leased by someone else
                    throw new LeaseNotCreatedException("Blob already exists or is leased by someone else.", exception)
                    {
                        Status = exception.Status,
                        ErrorCode = exception.ErrorCode
                    };
                }
                else
                {
                    throw new LeaseException("Unable to create lease blob", exception)
                    {
                        Status = exception.Status,
                        ErrorCode = exception.ErrorCode
                    };
                }
            }
        }

        public virtual async Task ReleaseAsync(string leaseId, CancellationToken cancellationToken)
        {
            try
            {
                var blobClient = _blobContainerClient.GetBlobClient(GetLockPath(_lockId));
                await GetBlobLeaseClient(blobClient, leaseId).ReleaseAsync(cancellationToken: cancellationToken);
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
                    throw new LeaseException("Could not release lock.", exception)
                    {
                        Status = exception.Status,
                        ErrorCode = exception.ErrorCode
                    };
                }
            }
        }

        public virtual async Task RenewAsync(string leaseId, CancellationToken cancellationToken)
        {
            try
            {
                var blobClient = _blobContainerClient.GetBlobClient(GetLockPath(_lockId));
                await GetBlobLeaseClient(blobClient, leaseId).RenewAsync(cancellationToken: cancellationToken);
            }
            catch (RequestFailedException exception)
            {
                if (exception.Status >= 500 && exception.Status < 600)
                {
                    throw new LeaseException("Server-side error renewing lock.", exception)
                    {
                        Status = exception.Status,
                        ErrorCode = exception.ErrorCode
                    };
                }
                else
                {
                    throw new LeaseException("Could not release lock.", exception);
                }
            }
        }

        public virtual async Task<string> GetLeaseOwner(CancellationToken cancellationToken)
        {
            try
            {
                var blobClient = _blobContainerClient.GetBlobClient(GetLockPath(_lockId));
                var propertiesResponse = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
                var blobProperties = propertiesResponse.Value;

                // if the lease is Available, then there is no current owner
                // (any existing owner value is the last owner that held the lease)
                if (blobProperties != null &&
                    blobProperties.LeaseState == LeaseState.Available &&
                    blobProperties.LeaseStatus == LeaseStatus.Unlocked)
                {
                    return null;
                }

                blobProperties.Metadata.TryGetValue(GenericDistributedLockManager.FunctionInstanceMetadataKey, out string owner);
                return owner;
            }
            catch (RequestFailedException exception)
            {
                if (exception.Status == 404)
                {
                    return null;
                }

                throw new LeaseException("Unable to get lease owner.", exception)
                {
                    Status = exception.Status,
                    ErrorCode = exception.ErrorCode
                };
            }
        }

        public virtual async Task SetLeaseOwner(string owner, string leaseId, CancellationToken cancellationToken)
        {
            try
            {
                var blobClient = _blobContainerClient.GetBlobClient(GetLockPath(_lockId));
                var propertiesResponse = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
                var blobProperties = propertiesResponse.Value;

                if (blobProperties != null)
                {
                    blobProperties.Metadata[GenericDistributedLockManager.FunctionInstanceMetadataKey] = owner;
                    await blobClient.SetMetadataAsync(
                        blobProperties.Metadata,
                        new BlobRequestConditions { LeaseId = leaseId },
                        cancellationToken: cancellationToken);
                }
            }
            catch (RequestFailedException exception)
            {
                if (exception.Status == 404)
                {
                    // blob not found - ignore the exception
                    return;
                }

                throw new LeaseException("Unable to set lease owner.", exception)
                {
                    Status = exception.Status,
                    ErrorCode = exception.ErrorCode
                };
            }
        }

        public virtual string GetLockId()
        {
            return _lockId;
        }

        protected virtual string GetLockPath(string lockId)
        {
            // lockId here is already in the format {accountName}/{functionDescriptor}.{scopeId}
            return string.Format(CultureInfo.InvariantCulture, "{0}/{1}", HostDirectoryNames.SingletonLocks, lockId);
        }

        protected virtual BlobLeaseClient GetBlobLeaseClient(BlobClient blobClient, string proposedLeaseId)
        {
            return blobClient.GetBlobLeaseClient(proposedLeaseId);
        }
    }

    public class SingletonAzureBlobLeaseProviderFactory : ILeaseProviderFactory
    {
        private BlobContainerClient _blobContainerClient;

        public SingletonAzureBlobLeaseProviderFactory(AzureStorageProvider azureStorageProvider, IOptions<JobHostInternalStorageOptions> options)
        {
            azureStorageProvider.TryGetBlobServiceClientFromConnection(out BlobServiceClient blobServiceClient, ConnectionStringNames.Storage);

            if (options.Value.InternalContainerName != null)
            {
                _blobContainerClient = blobServiceClient.GetBlobContainerClient(options.Value.InternalContainerName);
            }
            else if (options.Value.InternalSasBlobContainer != null)
            {
                _blobContainerClient = new BlobContainerClient(new Uri(options.Value.InternalSasBlobContainer));
            }
            else
            {
                _blobContainerClient = blobServiceClient.GetBlobContainerClient(HostContainerNames.Hosts);
            }
        }

        public ILeaseProvider GetLeaseProvider(string lockId, string accountOverride = null)
        {
            return new AzureBlobLeaseProvider(lockId, _blobContainerClient);
        }
    }
}
