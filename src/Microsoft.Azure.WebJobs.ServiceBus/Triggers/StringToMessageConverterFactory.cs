// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Converters;
using Microsoft.Azure.ServiceBus;

namespace Microsoft.Azure.WebJobs.ServiceBus.Triggers
{
    internal class StringTodMessageConverterFactory
    {
        public static IConverter<string, Message> Create(Type parameterType)
        {
            if (parameterType == typeof(Message) || parameterType == typeof(string))
            {
                return new StringToTextMessageConverter();
            }
            else if (parameterType == typeof(byte[]))
            {
                return new StringToBinarydMessageConverter();
            }
            else
            {
                return new StringToJsonMessageConverter();
            }
        }
    }
}
