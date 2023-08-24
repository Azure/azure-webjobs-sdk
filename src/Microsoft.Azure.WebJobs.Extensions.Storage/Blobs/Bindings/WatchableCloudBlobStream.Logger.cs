// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Bindings
{
    internal static class WatchableCloudBlobStreamLoggerExtension
    {
        private static readonly Action<ILogger, string, string, string, TimeSpan, long, Exception> _blobWriteAccess =
          LoggerMessage.Define<string, string, string, TimeSpan, long>(
              LogLevel.Debug,
              new EventId(2, nameof(BlobWriteAccess)),
              "BlobWriteAccess - Name: {blobName}, Type: {type}, ETag: {etag}, WriteTime: {writeTime}, BytesWritten: {bytesWritten}");

        // Name is of the format <ContainerName>/<BlobName>
        // Type is of the format <BlobType>/<ContentType>
        public static void BlobWriteAccess(this ILogger logger, string blobName, string type, string etag, TimeSpan writeTime, long bytesWritten)
        {
            _blobWriteAccess(logger, blobName, type, etag, writeTime, bytesWritten, null);
        }
    }
}
