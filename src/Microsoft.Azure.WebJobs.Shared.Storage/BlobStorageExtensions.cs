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
}