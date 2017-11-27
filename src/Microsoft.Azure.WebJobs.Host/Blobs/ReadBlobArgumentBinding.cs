﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.WindowsAzure.Storage;
using System.Threading;

namespace Microsoft.Azure.WebJobs.Host.Blobs
{
    internal static class ReadBlobArgumentBinding
    {
        public static Task<WatchableReadStream> TryBindStreamAsync(IStorageBlob blob, ValueBindingContext context)
        {
            return TryBindStreamAsync(blob, context.CancellationToken);
        }

        public static async Task<WatchableReadStream> TryBindStreamAsync(IStorageBlob blob, CancellationToken cancellationToken)
        {
            Stream rawStream;
            try
            {
                rawStream = await blob.OpenReadAsync(cancellationToken);
            }
            catch (StorageException exception)
            {
                // Testing generic error case since specific error codes are not available for FetchAttributes 
                // (HEAD request), including OpenRead. 
                if (!exception.IsNotFound())
                {
                    throw;
                }

                return null;
            }
            
            return new WatchableReadStream(rawStream);
        }

        public static TextReader CreateTextReader(WatchableReadStream watchableStream)
        {
            return new StreamReader(watchableStream);
        }
    }
}
