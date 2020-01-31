// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Storage;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Listeners
{
    internal static class CloudBlobClientExtensions
    {
        public static async Task<IEnumerable<IListBlobItem>> ListBlobsAsync(this CloudBlobClient client,
            string prefix, bool useFlatBlobListing, BlobListingDetails blobListingDetails, string operationName,
            IWebJobsExceptionHandler exceptionHandler, CancellationToken cancellationToken)
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
                OperationContext context = new OperationContext { ClientRequestID = Guid.NewGuid().ToString() };
                result = await TimeoutHandler.ExecuteWithTimeout(operationName, context.ClientRequestID, exceptionHandler, () =>
                {
                    return client.ListBlobsSegmentedAsync(prefix, useFlatBlobListing, blobListingDetails,
                        maxResults: null, currentToken: continuationToken, options: null, operationContext: context);
                });

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
