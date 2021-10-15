// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure.Storage.Blobs;

namespace Microsoft.Azure.WebJobs.Host.Storage
{
    /// <summary>
    /// Provides methods for creating Azure blob storage clients, ensuring all necessary configuration is applied.
    /// Implementations are responsible instantiating these clients and using desired options, credentials, or service URIs.
    /// </summary>
    public interface IAzureBlobStorageProvider
    {
        /// <summary>
        /// Attempts to create a client for the hosting container used for internal storage.
        /// </summary>
        /// <returns>A <see cref="BlobContainerClient"/> for the hosting container.</returns>
        bool TryCreateHostingBlobContainerClient(out BlobContainerClient blobContainerClient);

        /// <summary>
        /// Attempts to create the <see cref="BlobServiceClient"/> from the specified connection.
        /// </summary>
        /// <param name="connection">connection name to use.</param>
        /// <param name="client"><see cref="BlobServiceClient"/> to instantiate.</param>
        /// <returns>Whether the attempt was successful.</returns>
        bool TryCreateBlobServiceClientFromConnection(string connection, out BlobServiceClient client);
    }
}
