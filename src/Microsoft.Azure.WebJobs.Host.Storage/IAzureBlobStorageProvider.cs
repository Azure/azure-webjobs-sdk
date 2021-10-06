// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure.Storage.Blobs;

namespace Microsoft.Azure.WebJobs.Host.Storage
{
    /// <summary>
    /// Interface to retrieve Azure blob storage clients
    /// </summary>
    public interface IAzureBlobStorageProvider
    {
        /// <summary>
        /// Gets the WebJobs BlobContainerClient to use for internal operations with Blob storage.
        /// </summary>
        /// <returns>A <see cref="BlobContainerClient"/> for the desired container.</returns>
        BlobContainerClient GetWebJobsBlobContainerClient();

        /// <summary>
        /// Attempts to retrieve the <see cref="BlobServiceClient"/> from the specified connection.
        /// </summary>
        /// <param name="connection">connection name to use.</param>
        /// <param name="client"><see cref="BlobServiceClient"/> to instantiate.</param>
        /// <returns>Whether the attempt was successful.</returns>
        bool TryGetBlobServiceClientFromConnection(string connection, out BlobServiceClient client);
    }
}
