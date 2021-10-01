// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure.Storage.Blobs;

namespace Microsoft.Azure.WebJobs.Host.Storage
{
    /// <summary>
    /// Interface to retrieve Azure storage clients
    /// </summary>
    public interface IAzureStorageProvider
    {
        /// <summary>
        /// Checks whether the specified connection has an associated value or section.
        /// </summary>
        /// <param name="connection">Connection name to check.</param>
        /// <returns>Whether the connection name has an associated value or section.</returns>
        bool ConnectionExists(string connection);

        /// <summary>
        /// Attempts to retrieve the <see cref="BlobServiceClient"/> from the specified connection.
        /// </summary>
        /// <param name="client"><see cref="BlobServiceClient"/> to instantiate.</param>
        /// <param name="connection">connection name to use.</param>
        /// <returns>Whether the attempt was successful.</returns>
        bool TryGetBlobServiceClientFromConnection(out BlobServiceClient client, string connection);

        /// <summary>
        /// Retrieves a <see cref="BlobContainerClient"/> for the reserved WebJobs blob container.
        /// </summary>
        /// <returns><see cref="BlobContainerClient"/> for WebJobs operations.</returns>
        BlobContainerClient GetWebJobsBlobContainerClient();
    }
}
