// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using static Microsoft.Azure.EventHubs.EventData;

namespace Microsoft.Azure.WebJobs.EventHubs
{
    static internal class SystemPropertiesCollectionExtensions
    {
        internal static IDictionary<string, object> ToDictionary(this SystemPropertiesCollection collection)
        {
            return new Dictionary<string, object>()
            {
                { "SequenceNumber", collection.SequenceNumber},
                { "Offset", collection.Offset },
                { "PartitionKey", collection.PartitionKey },
                { "EnqueuedTimeUtc", collection.EnqueuedTimeUtc }
            };
        }
    }
}
