using System;
using System.Collections.Generic;
using System.Configuration;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    internal class AppSettingsBinding
    {
        public static IReadOnlyDictionary<string, object> CreateBindingData()
        {
            var data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            foreach (string item in ConfigurationManager.AppSettings)
            {
                data.Add(item, ConfigurationManager.AppSettings[item]);
            }

            return data;
        }
    }
}