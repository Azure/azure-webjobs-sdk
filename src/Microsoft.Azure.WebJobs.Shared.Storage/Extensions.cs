using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Shared.Protocol;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

internal static class StroageExtensions
{
    public static Task SetServicePropertiesAsync(this CloudBlobClient _sdk,  ServiceProperties properties, CancellationToken cancellationToken)
    {
        return _sdk.SetServicePropertiesAsync(properties, requestOptions: null, operationContext: null, cancellationToken: cancellationToken);
    }

    public static Task<ServiceProperties> GetServicePropertiesAsync(this CloudBlobClient _sdk, CancellationToken cancellationToken)
    {
        return _sdk.GetServicePropertiesAsync(options: null, operationContext: null, cancellationToken: cancellationToken);

    }
    public static Task<CloudBlobStream> OpenWriteAsync(this CloudBlockBlob _sdk, CancellationToken cancellationToken)
    {
        return _sdk.OpenWriteAsync(accessCondition: null, options: null, operationContext: null, cancellationToken: cancellationToken);
    }

    public static Task<string> DownloadTextAsync(this CloudBlockBlob _sdk, CancellationToken cancellationToken)
    {
        return _sdk.DownloadTextAsync(encoding: null, accessCondition: null, options: null, operationContext: null, cancellationToken: cancellationToken);
    }

    public static Task UploadTextAsync(this CloudBlockBlob _sdk, string content, Encoding encoding = null, AccessCondition accessCondition = null,
        BlobRequestOptions options = null, OperationContext operationContext = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        return _sdk.UploadTextAsync(content, encoding, accessCondition, options, operationContext,
         cancellationToken);
    }

    public static Task DeleteAsync(this CloudBlockBlob _sdk, CancellationToken cancellationToken)
    {
        return _sdk.DeleteAsync(DeleteSnapshotsOption.None, accessCondition: null, options: null, operationContext: null, cancellationToken: cancellationToken);
    }

    public static Task CreateIfNotExistsAsync(this CloudBlobContainer _sdk, CancellationToken cancellationToken)
    {
        return _sdk.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Off, options: null, operationContext: null, cancellationToken: cancellationToken);
    }

    public static Task<Stream> OpenReadAsync(this ICloudBlob _sdk, CancellationToken cancellationToken)
    {
        return _sdk.OpenReadAsync(accessCondition: null, options: null, operationContext: null);
    }

    public static Task<string> AcquireLeaseAsync(this CloudBlockBlob _sdk, TimeSpan? leaseTime, string proposedLeaseId,
        CancellationToken cancellationToken)
    {
        return _sdk.AcquireLeaseAsync(leaseTime, proposedLeaseId, accessCondition: null, options: null, operationContext: null, cancellationToken: cancellationToken);
    }

    public static Task FetchAttributesAsync(this ICloudBlob _sdk, CancellationToken cancellationToken)
    {
        return _sdk.FetchAttributesAsync(accessCondition: null, options: null, operationContext: null);
    }

    public static Task<ICloudBlob> GetBlobReferenceFromServerAsync(this CloudBlobContainer _sdk, string blobName, CancellationToken cancellationToken)
    {
        return _sdk.GetBlobReferenceFromServerAsync(blobName, accessCondition: null, options: null, operationContext: null, cancellationToken: cancellationToken);
    }
}

internal static class StorageExceptionExtensions
{

}