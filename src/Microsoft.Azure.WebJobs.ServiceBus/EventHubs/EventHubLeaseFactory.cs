// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Text;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    // EventProcessorHost requires a container name that the multiple host instances use to coordinate with each other and create leases. 
    // An event hub is described by an (namespace name, event hub path name). The mapping from the event hub to the container name
    // must be a one-to-one injective function such that:
    // 1. it's deterministic: when we scale out ot N separate instances, they must all compute the same container name for the same event hub. 
    // 2. it's collision free: different event hubs shouldn't share the same container. So we can't just use a hash. 
    //  Note that the event hub name is much more expressive than the storage container name, so we can't do a stateless collision-free transform. 
    // Hence we use a lookaside storage table to save the transform. 
    internal class EventHubLeaseFactory
    {
        // Name of Azure table that maps "eventHub" --> container name. 
        private const string TableName = "AzureFunctionsEventHubTable";
             
        // Get container name. Lease name that multiple eventhub instances use to coordinate with each other. 
        public static string GetContainerName(string eventHubName, string eventHubNamespace, string storageConnectionString)
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(storageConnectionString);
            var client = account.CreateCloudTableClient();
            var table = client.GetTableReference(TableName);

            string pk = EscapeStorageKey(eventHubNamespace);
            string rk = EscapeStorageKey(eventHubName);

            // Create a retrieve operation that takes a customer entity.
            var retrieveOperation = TableOperation.Retrieve<EventHubTableEntity>(pk, rk);

            // Execute the retrieve operation.
            Exception lastException = null;
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    var retrievedResult = table.Execute(retrieveOperation);

                    if (retrievedResult.Result != null)
                    {
                        // Common case when we're looking up an existing event hub. 
                        var entity = (EventHubTableEntity)retrievedResult.Result;
                        return entity.ContainerName;
                    }
                    else
                    {
                        // Storage container, restrict to: [a-z0-9\-]{3,63}, no consecutive dashes, start&end with alpha
                        // Guid is 32 chars long and ToString() yields lowercase.  
                        string newName = "eventhubs-" + Guid.NewGuid().ToString("N");

                        // Not found. We'll create one. 
                        var entity = new EventHubTableEntity { PartitionKey = pk, RowKey = rk, ContainerName = newName };

                        var insertOperation = TableOperation.Insert(entity);

                        // Execute the insert operation.
                        table.Execute(insertOperation);

                        // We successfully added it first. 
                        return entity.ContainerName;                        
                    }
                }
                catch (StorageException e)
                {
                    // Could be here from a 404 (table wasn't created) or a 429 (race condition trying to update an entity) 
                    table.CreateIfNotExists(); // just in case 
                    
                    // Retry lookup 
                    lastException = e;
                }
            }

            // Timeout looping 
            {
                string msg = string.Format(CultureInfo.InvariantCulture, "Failed to establish a lease container name for event hub '{0}': {1}", eventHubName, lastException);
                throw new InvalidOperationException(msg);
            }
        }     

        /// <summary>
        /// Escapes the storage character.
        /// </summary>
        /// <param name="character">The character.</param>
        private static string EscapeStorageCharacter(char character)
        {
            var ordinalValue = (ushort)character;
            if (ordinalValue < 0x100)
            {
                return string.Format(CultureInfo.InvariantCulture, ":{0:X2}", ordinalValue);
            }
            else
            {
                return string.Format(CultureInfo.InvariantCulture, "::{0:X4}", ordinalValue);
            }
        }

        /// <summary>
        /// Escapes the storage key.
        /// </summary>
        /// <param name="storageKey">The storage key.</param>
        public static string EscapeStorageKey(string storageKey)
        {
            StringBuilder escapedStorageKey = new StringBuilder(storageKey.Length);
            foreach (char c in storageKey)
            {
                if (!char.IsLetterOrDigit(c))
                {
                    escapedStorageKey.Append(EscapeStorageCharacter(c));
                }
                else
                {
                    escapedStorageKey.Append(c);
                }
            }

            return escapedStorageKey.ToString();
        }

        // The table entity that remembers our results. 
        // PK = event hub namespace
        // RK = eventhub name
        private class EventHubTableEntity : TableEntity
        {
            public string ContainerName { get; set; }
        }
    }
}