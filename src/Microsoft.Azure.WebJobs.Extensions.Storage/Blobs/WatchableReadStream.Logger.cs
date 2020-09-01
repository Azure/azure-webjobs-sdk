// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Blobs
{
    internal partial class WatchableReadStream
    {
        private static class Logger
        {
            // Keep these events in 1-100 range.

            private static readonly Action<ILogger, string, string, long, string, TimeSpan, long, Exception> _blobReadAccess =
              LoggerMessage.Define<string, string, long, string, TimeSpan, long>(
                  LogLevel.Debug,
                  new EventId(1, nameof(BlobReadAccess)),
                  "BlobReadAccess - Name: {name}, Type: {type}, Length: {length}, ETag: {etag}, ReadTime: {readTime}, BytesRead: {bytesRead}");

            // Name is of the format <ContainerName>/<BlobName>
            // Type is of the format <BlobType>/<ContentType>
            public static void BlobReadAccess(ILogger logger, string name, string type, long length, string etag, TimeSpan readTime, long bytesRead) => _blobReadAccess(logger, name, type, length, etag, readTime, bytesRead, null);
        }
    }
}
