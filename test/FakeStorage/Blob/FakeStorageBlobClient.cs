// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Shared.Protocol;

namespace FakeStorage
{
    internal class FakeStorageBlobClient : CloudBlobClient
    {
        public static Uri FakeUri = new Uri("https://fakeaccount.blob.core.windows.net");

        internal readonly MemoryBlobStore _store;
        private readonly StorageCredentials _credentials;

        public FakeStorageBlobClient(FakeAccount account)
            : base(FakeUri)
        {
            _store = account._blobStore;
            _credentials = account._creds;
        }

        internal Uri GetContainerUri(string containerName)
        {
            return new Uri(this.BaseUri.ToString() +  containerName); // Uri already has trailing slash 
        }

        public override bool Equals(object obj)
        {
            if (obj is FakeStorageBlobClient other)
            {
                return this.BaseUri == other.BaseUri;
            }
            return false;
        }

        public override Task<ICloudBlob> GetBlobReferenceFromServerAsync(Uri blobUri)
        {
            throw new NotImplementedException();
            return base.GetBlobReferenceFromServerAsync(blobUri);
        }

        public override Task<ICloudBlob> GetBlobReferenceFromServerAsync(Uri blobUri, AccessCondition accessCondition, BlobRequestOptions options, OperationContext operationContext)
        {
            throw new NotImplementedException();
            return base.GetBlobReferenceFromServerAsync(blobUri, accessCondition, options, operationContext);
        }

        public override Task<ICloudBlob> GetBlobReferenceFromServerAsync(StorageUri blobUri, AccessCondition accessCondition, BlobRequestOptions options, OperationContext operationContext)
        {
            throw new NotImplementedException();
            return base.GetBlobReferenceFromServerAsync(blobUri, accessCondition, options, operationContext);
        }

        public override Task<ICloudBlob> GetBlobReferenceFromServerAsync(StorageUri blobUri, AccessCondition accessCondition, BlobRequestOptions options, OperationContext operationContext, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
            return base.GetBlobReferenceFromServerAsync(blobUri, accessCondition, options, operationContext, cancellationToken);
        }

        public override CloudBlobContainer GetContainerReference(string containerName)
        {
            return new FakeStorageBlobContainer(this, containerName);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override CloudBlobContainer GetRootContainerReference()
        {
            throw new NotImplementedException();
            return base.GetRootContainerReference();
        }

        public Task<ServiceProperties> GetServicePropertiesAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
            ServiceProperties properties = _store.GetServiceProperties();
            return Task.FromResult(properties);
        }

        public override Task<ServiceProperties> GetServicePropertiesAsync()
        {
            throw new NotImplementedException();
            return base.GetServicePropertiesAsync();
        }

        public override Task<ServiceProperties> GetServicePropertiesAsync(BlobRequestOptions options, OperationContext operationContext)
        {
            throw new NotImplementedException();
            return base.GetServicePropertiesAsync(options, operationContext);
        }

        public override Task<ServiceProperties> GetServicePropertiesAsync(BlobRequestOptions options, OperationContext operationContext, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
            return base.GetServicePropertiesAsync(options, operationContext, cancellationToken);
        }

        public override Task<ServiceStats> GetServiceStatsAsync()
        {
            throw new NotImplementedException();
            return base.GetServiceStatsAsync();
        }

        public override Task<ServiceStats> GetServiceStatsAsync(BlobRequestOptions options, OperationContext operationContext)
        {
            throw new NotImplementedException();
            return base.GetServiceStatsAsync(options, operationContext);
        }

        public override Task<ServiceStats> GetServiceStatsAsync(BlobRequestOptions options, OperationContext operationContext, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
            return base.GetServiceStatsAsync(options, operationContext, cancellationToken);
        }

        public override Task<BlobResultSegment> ListBlobsSegmentedAsync(string prefix, BlobContinuationToken currentToken)
        {
            throw new NotImplementedException();
            return base.ListBlobsSegmentedAsync(prefix, currentToken);
        }

        public override Task<BlobResultSegment> ListBlobsSegmentedAsync(string prefix, bool useFlatBlobListing, BlobListingDetails blobListingDetails, int? maxResults, BlobContinuationToken currentToken, BlobRequestOptions options, OperationContext operationContext)
        {
            throw new NotImplementedException();
            return base.ListBlobsSegmentedAsync(prefix, useFlatBlobListing, blobListingDetails, maxResults, currentToken, options, operationContext);
        }

        public override Task<ContainerResultSegment> ListContainersSegmentedAsync(BlobContinuationToken currentToken)
        {
            throw new NotImplementedException();
            return base.ListContainersSegmentedAsync(currentToken);
        }

        public override Task<ContainerResultSegment> ListContainersSegmentedAsync(string prefix, BlobContinuationToken currentToken)
        {
            throw new NotImplementedException();
            return base.ListContainersSegmentedAsync(prefix, currentToken);
        }

        public override Task<ContainerResultSegment> ListContainersSegmentedAsync(string prefix, ContainerListingDetails detailsIncluded, int? maxResults, BlobContinuationToken currentToken, BlobRequestOptions options, OperationContext operationContext)
        {
            throw new NotImplementedException();
            return base.ListContainersSegmentedAsync(prefix, detailsIncluded, maxResults, currentToken, options, operationContext);
        }

        public override Task<ContainerResultSegment> ListContainersSegmentedAsync(string prefix, ContainerListingDetails detailsIncluded, int? maxResults, BlobContinuationToken currentToken, BlobRequestOptions options, OperationContext operationContext, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
            return base.ListContainersSegmentedAsync(prefix, detailsIncluded, maxResults, currentToken, options, operationContext, cancellationToken);
        }

        public Task SetServicePropertiesAsync(ServiceProperties properties, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
            _store.SetServiceProperties(properties);
            return Task.FromResult(0);
        }

        public override Task SetServicePropertiesAsync(ServiceProperties properties)
        {
            throw new NotImplementedException();
            return base.SetServicePropertiesAsync(properties);
        }

        public override Task SetServicePropertiesAsync(ServiceProperties properties, BlobRequestOptions requestOptions, OperationContext operationContext)
        {
            throw new NotImplementedException();
            return base.SetServicePropertiesAsync(properties, requestOptions, operationContext);
        }

        public override Task SetServicePropertiesAsync(ServiceProperties properties, BlobRequestOptions requestOptions, OperationContext operationContext, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
            return base.SetServicePropertiesAsync(properties, requestOptions, operationContext, cancellationToken);
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }
}
