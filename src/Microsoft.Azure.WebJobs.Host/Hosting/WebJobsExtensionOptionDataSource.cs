// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Host.Hosting
{
    public class WebJobsExtensionOptionDataSource : IWebJobsExtensionOptionDataSource
    {
        private static ConcurrentDictionary<string, Dictionary<string, object>> _extensiosConfigs = new ConcurrentDictionary<string, Dictionary<string, object>>();

        public void Clear()
        {
            _extensiosConfigs.Clear();
        }

        public JObject GetOptions(string section)
        {
            var json = new JObject();
            var sectionOptions = _extensiosConfigs[section];
            if (sectionOptions == null)
            {
                return json;
            }

            foreach (var kv in sectionOptions)
            {
                json.Add(kv.Key, JObject.Parse(JsonConvert.SerializeObject(kv.Value)));
            }
            return json;
        }

        public JObject GetOptions()
        {
            var json = new JObject();
            foreach (var section in _extensiosConfigs)
            {
                foreach (var subSection in section.Value)
                {
                    var subSectionJson = new JObject();
                    subSectionJson.Add(subSection.Key, JObject.Parse(JsonConvert.SerializeObject(subSection.Value)));
                    json.Add(section.Key, subSectionJson);
                }
            }
            return json;
        }

        public object Register(string section, string subSection, object config)
        {
            return _extensiosConfigs.AddOrUpdate(section, (k) =>
            {
                var firstDictionary = new Dictionary<string, object>();
                firstDictionary.Add(subSection, config);
                return firstDictionary;
            }, (k, v) => 
            { 
                v.Add(subSection, config); 
                return v; 
            });
        }
    }
}
