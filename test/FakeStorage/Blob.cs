// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Shared.Protocol;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FakeStorage
{
    public class FakeBlobClient : CloudBlobClient
    {
        public static Uri FakeUri = new Uri("http://localhost:10000/fakeaccount/");

        public FakeBlobClient(FakeAccount account) :
            base(FakeUri, account._creds)
        {
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override Task<ICloudBlob> GetBlobReferenceFromServerAsync(Uri blobUri)
        {
            return base.GetBlobReferenceFromServerAsync(blobUri);
        }

        public override Task<ICloudBlob> GetBlobReferenceFromServerAsync(Uri blobUri, AccessCondition accessCondition, BlobRequestOptions options, OperationContext operationContext)
        {
            return base.GetBlobReferenceFromServerAsync(blobUri, accessCondition, options, operationContext);
        }

        public override Task<ICloudBlob> GetBlobReferenceFromServerAsync(StorageUri blobUri, AccessCondition accessCondition, BlobRequestOptions options, OperationContext operationContext)
        {
            return base.GetBlobReferenceFromServerAsync(blobUri, accessCondition, options, operationContext);
        }

        public override Task<ICloudBlob> GetBlobReferenceFromServerAsync(StorageUri blobUri, AccessCondition accessCondition, BlobRequestOptions options, OperationContext operationContext, CancellationToken cancellationToken)
        {
            return base.GetBlobReferenceFromServerAsync(blobUri, accessCondition, options, operationContext, cancellationToken);
        }

        public override CloudBlobContainer GetContainerReference(string containerName)
        {
            return base.GetContainerReference(containerName);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override CloudBlobContainer GetRootContainerReference()
        {
            return base.GetRootContainerReference();
        }

        public override Task<ServiceProperties> GetServicePropertiesAsync()
        {
            return base.GetServicePropertiesAsync();
        }

        public override Task<ServiceProperties> GetServicePropertiesAsync(BlobRequestOptions options, OperationContext operationContext)
        {
            return base.GetServicePropertiesAsync(options, operationContext);
        }

        public override Task<ServiceProperties> GetServicePropertiesAsync(BlobRequestOptions options, OperationContext operationContext, CancellationToken cancellationToken)
        {
            return base.GetServicePropertiesAsync(options, operationContext, cancellationToken);
        }

        public override Task<ServiceStats> GetServiceStatsAsync()
        {
            return base.GetServiceStatsAsync();
        }

        public override Task<ServiceStats> GetServiceStatsAsync(BlobRequestOptions options, OperationContext operationContext)
        {
            return base.GetServiceStatsAsync(options, operationContext);
        }

        public override Task<ServiceStats> GetServiceStatsAsync(BlobRequestOptions options, OperationContext operationContext, CancellationToken cancellationToken)
        {
            return base.GetServiceStatsAsync(options, operationContext, cancellationToken);
        }

        public override Task<BlobResultSegment> ListBlobsSegmentedAsync(string prefix, BlobContinuationToken currentToken)
        {
            return base.ListBlobsSegmentedAsync(prefix, currentToken);
        }

        public override Task<BlobResultSegment> ListBlobsSegmentedAsync(string prefix, bool useFlatBlobListing, BlobListingDetails blobListingDetails, int? maxResults, BlobContinuationToken currentToken, BlobRequestOptions options, OperationContext operationContext)
        {
            return base.ListBlobsSegmentedAsync(prefix, useFlatBlobListing, blobListingDetails, maxResults, currentToken, options, operationContext);
        }

        public override Task<ContainerResultSegment> ListContainersSegmentedAsync(BlobContinuationToken currentToken)
        {
            return base.ListContainersSegmentedAsync(currentToken);
        }

        public override Task<ContainerResultSegment> ListContainersSegmentedAsync(string prefix, BlobContinuationToken currentToken)
        {
            return base.ListContainersSegmentedAsync(prefix, currentToken);
        }

        public override Task<ContainerResultSegment> ListContainersSegmentedAsync(string prefix, ContainerListingDetails detailsIncluded, int? maxResults, BlobContinuationToken currentToken, BlobRequestOptions options, OperationContext operationContext)
        {
            return base.ListContainersSegmentedAsync(prefix, detailsIncluded, maxResults, currentToken, options, operationContext);
        }

        public override Task<ContainerResultSegment> ListContainersSegmentedAsync(string prefix, ContainerListingDetails detailsIncluded, int? maxResults, BlobContinuationToken currentToken, BlobRequestOptions options, OperationContext operationContext, CancellationToken cancellationToken)
        {
            return base.ListContainersSegmentedAsync(prefix, detailsIncluded, maxResults, currentToken, options, operationContext, cancellationToken);
        }

        public override Task SetServicePropertiesAsync(ServiceProperties properties)
        {
            return base.SetServicePropertiesAsync(properties);
        }

        public override Task SetServicePropertiesAsync(ServiceProperties properties, BlobRequestOptions requestOptions, OperationContext operationContext)
        {
            return base.SetServicePropertiesAsync(properties, requestOptions, operationContext);
        }

        public override Task SetServicePropertiesAsync(ServiceProperties properties, BlobRequestOptions requestOptions, OperationContext operationContext, CancellationToken cancellationToken)
        {
            return base.SetServicePropertiesAsync(properties, requestOptions, operationContext, cancellationToken);
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }
}