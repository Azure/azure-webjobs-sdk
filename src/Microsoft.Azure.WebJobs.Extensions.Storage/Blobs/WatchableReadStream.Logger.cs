// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Blobs
{
    internal static class WatchableReadStreamLoggerExtension
    {
        private static readonly Action<ILogger, string, string, long, string, TimeSpan, long, Exception> _blobReadAccess =
          LoggerMessage.Define<string, string, long, string, TimeSpan, long>(
              LogLevel.Debug,
              new EventId(1, nameof(BlobReadAccess)),
              "BlobReadAccess - BlobName: {blobName}, Type: {type}, Length: {length}, ETag: {etag}, ReadTime: {readTime}, BytesRead: {bytesRead}");

        // Name is of the format <ContainerName>/<BlobName>
        // Type is of the format <BlobType>/<ContentType>
        public static void BlobReadAccess(this ILogger logger, string blobName, string type, long length, string etag, TimeSpan readTime, long bytesRead)
        {
            _blobReadAccess(logger, blobName, type, length, etag, readTime, bytesRead, null);
        }
    }
}
