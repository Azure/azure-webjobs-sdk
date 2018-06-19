// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Listeners
{
    internal static class CloudBlobClientExtensions
    {
        public static async Task<IEnumerable<IListBlobItem>> ListBlobsAsync(this CloudBlobClient client,
            string prefix, bool useFlatBlobListing, BlobListingDetails blobListingDetails,
            CancellationToken cancellationToken)
        {
            if (client == null)
            {
                throw new ArgumentNullException("client");
            }

            List<IListBlobItem> allResults = new List<IListBlobItem>();
            BlobContinuationToken continuationToken = null;
            BlobResultSegment result;

            do
            {
                result = await client.ListBlobsSegmentedAsync(prefix, useFlatBlobListing, blobListingDetails,
                    maxResults: null, currentToken: continuationToken, options: null, operationContext: null
                    );

                if (result != null)
                {
                    IEnumerable<IListBlobItem> currentResults = result.Results;
                    if (currentResults != null)
                    {
                        allResults.AddRange(currentResults);
                    }

                    continuationToken = result.ContinuationToken;
                }
            }
            while (result != null && continuationToken != null);

            return allResults;
        }    
    }
}
