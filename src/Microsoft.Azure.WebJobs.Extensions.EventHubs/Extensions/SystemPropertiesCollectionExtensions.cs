// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using static Microsoft.Azure.EventHubs.EventData;

namespace Microsoft.Azure.WebJobs.EventHubs
{
    static internal class SystemPropertiesCollectionExtensions
    {
        internal static IDictionary<string, object> ToDictionary(this SystemPropertiesCollection collection)
        {
            IDictionary<string, object> modifiedDictionary = collection;
            
            // Following is needed to maintain structure of bindingdata: https://github.com/Azure/azure-webjobs-sdk/pull/1849
            modifiedDictionary["SequenceNumber"] = collection.SequenceNumber;
            modifiedDictionary["Offset"] = collection.Offset;
            modifiedDictionary["PartitionKey"] = collection.PartitionKey;
            modifiedDictionary["EnqueuedTimeUtc"] = collection.EnqueuedTimeUtc;
            return modifiedDictionary;
        }
    }
}
