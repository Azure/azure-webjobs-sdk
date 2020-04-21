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

        public InstrumentableObjectMetadata(bool isTriggerParameter, bool isCacheTriggered)
        {
            _properties = new Dictionary<string, string>();
            IsTriggerParameter = isTriggerParameter;
            IsCacheTriggered = isCacheTriggered;
            if (!IsTriggerParameter && IsCacheTriggered)
            {
                throw new Exception("Cannot be cache triggered if it is not a trigger parameter");
            }
        }

        public bool IsTriggerParameter { get; private set; }
        
        public bool IsCacheTriggered { get; private set; }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.Append("IsTriggerParameter: ");
            stringBuilder.Append(IsTriggerParameter);
            stringBuilder.Append(", IsCacheTriggered: ");
            stringBuilder.Append(IsCacheTriggered);

            foreach (KeyValuePair<string, string> entry in _properties)
            {
                stringBuilder.Append(", ");
                stringBuilder.Append(entry.Key);
                stringBuilder.Append(": ");
                stringBuilder.Append(entry.Value);
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
