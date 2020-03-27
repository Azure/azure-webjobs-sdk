// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

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
                // TODO not doing new line for now, don't now how it shows up in logs
                stringBuilder.Append(", ");
            }

            return stringBuilder.ToString();
        }

        public void Add(string key, string value)
        {
            _properties.Add(key, value);
        }
    }
}
