// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    internal class TriggerMetadataProvider : ITriggerMetadataProvider
    {
        private JArray _fucntionMetadata;
        private Dictionary<string, IEnumerable<object>> _metadataProperties;

        public TriggerMetadataProvider(JArray fucntionMetadata, Dictionary<string, IEnumerable<object>> metadataProperties)
        {
            _fucntionMetadata = fucntionMetadata;
            _metadataProperties = metadataProperties;
        }

        public IEnumerable<TriggerMetadata> GetTriggersMetadata(string triggerType)
        {
            List<TriggerMetadata> scalerContexts = new List<TriggerMetadata>();
            foreach (JObject function in _fucntionMetadata)
            {
                if (!MatchTriggerType(function, triggerType))
                {
                    continue;
                }

                var triggerMetadata = new TriggerMetadata()
                {
                    Value = function
                };
                string fucntionName = function["functionName"].ToString();
                if (_metadataProperties.TryGetValue(fucntionName, out var properties))
                {
                    triggerMetadata.AddProperties(properties);
                }

                scalerContexts.Add(triggerMetadata);
            }
            return scalerContexts;
        }

        private bool MatchTriggerType(JObject function, string triggerType)
        {
            string type = (string)function["type"];
            return string.Equals(type, triggerType, StringComparison.OrdinalIgnoreCase);
        }
    }
}
