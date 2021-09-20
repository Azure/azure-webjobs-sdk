// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure.Storage.Blobs;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Interface to retrieve BlobServiceClient objects
    /// </summary>
    public interface IAzureStorageProvider
    {
        bool ConnectionExists(string connection);

        bool TryGetBlobServiceClientFromConnection(out BlobServiceClient client, string connection);

        BlobContainerClient GetWebJobsBlobContainerClient();
    }
}
