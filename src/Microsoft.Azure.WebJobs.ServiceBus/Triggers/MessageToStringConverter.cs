// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.InteropExtensions;

namespace Microsoft.Azure.WebJobs.ServiceBus.Triggers
{
    internal class MessageToStringConverter : IAsyncConverter<Message, string>
    {
        public async Task<string> ConvertAsync(Message input, CancellationToken cancellationToken)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }
            if (input.Body == null)
            {
                return null;
            }
            Stream stream = new MemoryStream(input.Body);

            TextReader reader = new StreamReader(stream, StrictEncodings.Utf8);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return await reader.ReadToEndAsync();
                }
                catch (DecoderFallbackException)
                {
                    // we'll try again below
                }

                // We may get here if the message is a string yet was DataContract-serialized when created. We'll
                // try to deserialize it here using GetBody<string>(). This may fail as well, in which case we'll
                // provide a decent error.

                try
                {
                    return input.GetBody<string>();
                }
                catch (Exception exception)
                {
                    string contentType = input.ContentType ?? "null";
                    string msg = string.Format(CultureInfo.InvariantCulture, "The Message with ContentType '{0}' failed to deserialize to a string with the message: '{1}'",
                        contentType, exception.Message);

                    throw new InvalidOperationException(msg, exception);
                }
            }
            finally
            {
                if (stream != null)
                {
                    stream.Dispose();
                }
                if (reader != null)
                {
                    reader.Dispose();
                }
            }
        }
    }
}
