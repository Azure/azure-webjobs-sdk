using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using static Microsoft.Azure.EventHubs.EventData;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    static internal class SystemPropertiesCollectionExtensions
    {
        internal static IDictionary<string, object> ToDictionary(this SystemPropertiesCollection collection)
        {
            return JObject.FromObject(collection).ToObject<IDictionary<string, object>>();
        }
    }
}
