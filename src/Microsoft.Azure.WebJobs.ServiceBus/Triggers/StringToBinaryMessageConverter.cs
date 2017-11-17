// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Azure.WebJobs.Host.Converters;
using Microsoft.Azure.ServiceBus;


namespace Microsoft.Azure.WebJobs.ServiceBus.Triggers
{
    internal class StringToBinarydMessageConverter : IConverter<string, Message>
    {
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        public Message Convert(string input)
        {
            byte[] contents = StrictEncodings.Utf8.GetBytes(input);
            Message message = new Message(contents);
            message.ContentType = ContentTypes.ApplicationOctetStream;
            return message;
        }
    }
}
