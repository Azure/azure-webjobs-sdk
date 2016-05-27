﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Converters;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.Azure.WebJobs.ServiceBus.Triggers
{
    internal class BrokeredMessageToByteArrayConverter : IAsyncConverter<BrokeredMessage, byte[]>
    {
        public async Task<byte[]> ConvertAsync(BrokeredMessage input, CancellationToken cancellationToken)
        {
            if (input.ContentType == ContentTypes.ApplicationOctetStream ||
                input.ContentType == ContentTypes.TextPlain ||
                input.ContentType == ContentTypes.ApplicationJson)
            {
                using (MemoryStream outputStream = new MemoryStream())
                using (Stream inputStream = input.GetBody<Stream>())
                {
                    if (inputStream == null)
                    {
                        return null;
                    }

                    const int DefaultBufferSize = 4096;
                    await inputStream.CopyToAsync(outputStream, DefaultBufferSize, cancellationToken);
                    return outputStream.ToArray();
                }
            }
            else
            {
                return input.GetBody<byte[]>();
            }
        }
    }
}
