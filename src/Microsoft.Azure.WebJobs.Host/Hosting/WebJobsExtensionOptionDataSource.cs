// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Host.Hosting
{
    public class WebJobsExtensionOptionDataSource : IWebJobsExtensionOptionDataSource
    {
        private static ConcurrentDictionary<string, object> _extensiosConfigs = new ConcurrentDictionary<string, object>();

        public void Clear()
        {
            _extensiosConfigs.Clear();
        }

        public IReadOnlyDictionary<string, object> GetExtensionConfigs()
        {
            return _extensiosConfigs.ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public JObject GetOptions()
        {
            var json = new JObject();
            foreach (var section in _extensiosConfigs)
            {
                json.Add(section.Key, JObject.Parse(JsonConvert.SerializeObject(section.Value)));
            }
            return json;
        }

        public object Register(string section, object config)
        {
            return _extensiosConfigs.AddOrUpdate(section, config, (k, v) => v);
        }
    }
}
