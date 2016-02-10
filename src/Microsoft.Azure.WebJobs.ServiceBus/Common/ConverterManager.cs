// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using System;
using System.Text;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    // Central 
    // $$$ this would be good to have in the core SDK so that any extension can add to it. 
    // Rationalize with IConverter
    internal class ConverterManager
    {
        public static EventData ConvertString2EventData(string input)
        {
            var eventData = new EventData(Encoding.UTF8.GetBytes(input));
            return eventData;
        }

        public static EventData ConvertUser2EventData<TSrc>(TSrc input)
        {
            string json = JsonConvert.SerializeObject(input);
            return ConvertString2EventData(json);
        }


        public Func<TSrc, TDest> GetConverter<TSrc, TDest>()
        {
            if (typeof(TDest) == typeof(EventData))
            {
                if (typeof(TSrc) == typeof(string))
                {
                    Func<string, EventData> func = ConvertString2EventData;

                    return src =>
                    {
                        string input = (string)(object)src;
                        var result = func(input);
                        return (TDest)(object)result;
                    };
                }
                else {
                    // For user-defined types, try JSON deserialization 
                    Func<TSrc, EventData> func = ConvertUser2EventData;
                    return src =>
                    {
                        var result = func(src);
                        return (TDest)(object)result;
                    };
                }

            }

            // No conversion  $$$
            throw new NotImplementedException();
        }
    }
}