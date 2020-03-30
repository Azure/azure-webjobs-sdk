// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Protocols;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    public class InstrumentableObjectMetadata
    {
        private Dictionary<string, string> _properties;

        public InstrumentableObjectMetadata()
        {
            _properties = new Dictionary<string, string>();
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();

            foreach (KeyValuePair<string, string> entry in _properties)
            {
                stringBuilder.Append(entry.Key);
                stringBuilder.Append(": ");
                stringBuilder.Append(entry.Value);
                stringBuilder.Append(", ");
            }

            return stringBuilder.ToString();
        }

        public void Add(string key, string value)
        {
            _properties.RemoveIfContainsKey(key);
            _properties.Add(key, value);
        }
        
        public bool TryGetValue(string key, out string value)
        {
            return _properties.TryGetValue(key, out value);
        }
    }
}
