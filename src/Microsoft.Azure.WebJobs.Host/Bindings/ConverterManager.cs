﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs
{
    // Concrete implementation of IConverterManager
    internal class ConverterManager : IConverterManager
    {
        // Map from <TSrc,TDest> to a converter function. 
        private Dictionary<string, object> _funcs = new Dictionary<string, object>();

        public ConverterManager()
        {
            this.AddConverter<byte[], string>(DefaultByteArray2String);
        }

        private static string DefaultByteArray2String(byte[] bytes)
        {
            string str = Encoding.UTF8.GetString(bytes);
            return str;
        }

        private static string GetKey<TSrc, TDest>()
        {
            return typeof(TSrc).FullName + "|" + typeof(TDest).FullName;
        }

        public void AddConverter<TSrc, TDest>(Func<TSrc, TDest> converter)
        {
            string key = GetKey<TSrc, TDest>();
            _funcs[key] = converter;
        }

        private Func<TSrc, TDest> TryGetConverter<TSrc, TDest>()
        {
            string key = GetKey<TSrc, TDest>();

            object obj;
            if (!_funcs.TryGetValue(key, out obj))
            {
                return null;
            }
            var func2 = obj as Func<TSrc, TDest>;
            return func2;
        }

        public Func<TSrc, TDest> GetConverter<TSrc, TDest>()
        {
            // Give precedence to exact matches.
            // this lets callers override any other rules (like JSON binding) 

            // TSrc --> TDest
            Func<TSrc, TDest> exactMatch = TryGetConverter<TSrc, TDest>();
            if (exactMatch != null)
            {
                return exactMatch;
            }

            // Object --> TDest
            // Catch all for any conversion to TDest
            Func<object, TDest> objConversion = TryGetConverter<object, TDest>();
            if (objConversion != null)
            {
                return (src) =>
                {
                    var result = objConversion(src);
                    return result;
                };
            }

            // Inheritence (also covers idempotency)
            if (typeof(TDest).IsAssignableFrom(typeof(TSrc)))
            {
                return (src) =>
                {
                    object obj = (object)src;
                    return (TDest)obj;
                };
            }

            // string --> TDest
            Func<string, TDest> fromString = TryGetConverter<string, TDest>();
            if (fromString == null)
            {
                string msg = string.Format(CultureInfo.CurrentCulture, "Can't convert from {0} to {1}", "string", typeof(TDest).FullName);
                throw new NotImplementedException(msg);
            }

            // String --> TDest
            if (typeof(TSrc) == typeof(string))
            {
                return src =>
                {
                    var result = fromString((string)(object)src);
                    return result;
                };
            }

            // Allow some well-defined intermediate conversions. 
            // If this is "wrong" for your type, then it should provide an exact match to override.

            // Byte[] --[builtin]--> String --> TDest
            if (typeof(TSrc) == typeof(byte[]))
            {
                Func<byte[], string> bytes2string = TryGetConverter<byte[], string>();

                return src =>
                {
                    byte[] bytes = (byte[])(object)src;
                    string str = bytes2string(bytes);
                    var result = fromString(str);
                    return result;
                };
            }

            // TSrc --[Json]--> string --> TDest
            return (src) =>
            {
                string json = JsonConvert.SerializeObject(src);
                TDest obj = fromString(json);
                return obj;
            };
        }
    } // end class ConverterManager
}