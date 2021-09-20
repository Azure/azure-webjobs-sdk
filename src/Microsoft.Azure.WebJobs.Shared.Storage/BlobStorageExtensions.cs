// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

internal static class BlobStorageExtensions
{
    public static async Task UploadTextAsync(this BlobClient blobClient, string content)
    {
        using (Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
        {
            await blobClient.UploadAsync(stream, overwrite: true);
        }
    }

    public static async Task UploadTextAsync(this BlockBlobClient blockBlobClient, string content)
    {
        using (Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
        {
            await blockBlobClient.UploadAsync(stream);
        }
    }

    public static async Task<string> DownloadTextAsync(this BlobClient blobClient)
    {
        var downloadResponse = await blobClient.DownloadAsync();
        using (StreamReader reader = new StreamReader(downloadResponse.Value.Content, true))
        {
            string content = reader.ReadToEnd();
            return content;
        }
    }
#if false // $$$
    public static Task SetServicePropertiesAsync(this CloudBlobClient sdk, ServiceProperties properties, CancellationToken cancellationToken)
    {
        return sdk.SetServicePropertiesAsync(properties, requestOptions: null, operationContext: null, cancellationToken: cancellationToken);
    }

    public static Task<ServiceProperties> GetServicePropertiesAsync(this CloudBlobClient sdk, CancellationToken cancellationToken)
    {
        return sdk.GetServicePropertiesAsync(cancellationToken);
    }

    public static Task<CloudBlobStream> OpenWriteAsync(this CloudBlockBlob sdk, CancellationToken cancellationToken)
    {
        return sdk.OpenWriteAsync(accessCondition: null, options: null, operationContext: null, cancellationToken: cancellationToken);
    }

    public static Task<string> DownloadTextAsync(this CloudBlockBlob sdk, CancellationToken cancellationToken)
    {
        return sdk.DownloadTextAsync(encoding: null, accessCondition: null, options: null, operationContext: null, cancellationToken: cancellationToken);
    }

    public static Task UploadTextAsync(this CloudBlockBlob sdk, string content, Encoding encoding = null, AccessCondition accessCondition = null,
        BlobRequestOptions options = null, OperationContext operationContext = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        return sdk.UploadTextAsync(content, encoding, accessCondition, options, operationContext,
         cancellationToken);
    }

    public static Task DeleteAsync(this CloudBlockBlob sdk, CancellationToken cancellationToken)
    {
        return sdk.DeleteAsync(DeleteSnapshotsOption.None, accessCondition: null, options: null, operationContext: null, cancellationToken: cancellationToken);
    }

    public static Task CreateIfNotExistsAsync(this CloudBlobContainer sdk, CancellationToken cancellationToken)
    {
        return sdk.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Off, options: null, operationContext: null, cancellationToken: cancellationToken);
    }

    public static Task<Stream> OpenReadAsync(this ICloudBlob sdk, CancellationToken cancellationToken)
    {
        return sdk.OpenReadAsync(accessCondition: null, options: null, operationContext: null, cancellationToken: cancellationToken);
    }

    public static Task<string> AcquireLeaseAsync(this CloudBlockBlob sdk, TimeSpan? leaseTime, string proposedLeaseId,
        CancellationToken cancellationToken)
    {
        return sdk.AcquireLeaseAsync(leaseTime, proposedLeaseId, accessCondition: null, options: null, operationContext: null, cancellationToken: cancellationToken);
    }

    public static Task FetchAttributesAsync(this ICloudBlob sdk, CancellationToken cancellationToken)
    {
        return sdk.FetchAttributesAsync(accessCondition: null, options: null, operationContext: null, cancellationToken: cancellationToken);
    }

    public static Task<ICloudBlob> GetBlobReferenceFromServerAsync(this CloudBlobContainer sdk, string blobName, CancellationToken cancellationToken)
    {
        return sdk.GetBlobReferenceFromServerAsync(blobName, accessCondition: null, options: null, operationContext: null, cancellationToken: cancellationToken);
    }
#endif
}