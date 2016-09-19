// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    // EventProcessorHost requires a container name that the multiple host instances use to coordinate with each other and create leases. 
    // An event hub is described by an (namespace name, event hub path name). The mapping from the event hub to the container name
    // must be a one-to-one injective function such that:
    // 1. it's deterministic: when we scale out ot N separate instances, they must all compute the same container name for the same event hub. 
    // 2. it's collision free: different event hubs shouldn't share the same container. So we can't just use a hash. 
    //  Note that the event hub name is much more expressive than the storage container name, so we can't do a stateless collision-free transform. 
    // Hence we use a lookaside storage to save the transform. 
    internal class EventHubLeaseFactory
    {
        // Name of Azure blob that maps "eventHub" --> container name. 
        private const string InternalContainerName = "azure-webjobs-hosts";
        private const string InternalBlobPath = "eventhub/leasenames.json";

        // Concurrent safe mutation of a blob. 
        // Apply mutator to update the blob (as a deserialized JSON contents). 
        // If blob doesn't exist yet, then this allocates a new TBlob() object and calls the mutator on that. 
        // Mutator also extracts a value and returns it. 
        private static TResult MutateBlob<TBlob, TResult>(CloudBlockBlob blob, Func<TBlob, TResult> mutator) where TBlob : new()
        {
            int maxRetries = 20;
            int retry = 0;

            while (true)
            {
                if (retry > maxRetries)
                {
                    throw new InvalidOperationException("Failed to update blob after " + retry + " retries.");
                }
                retry++;

                string oldJson = null;
                TBlob obj;
                AccessCondition access;

                try
                {
                    oldJson = blob.DownloadText(); // 404 if not yet exists.

                    string etag = blob.Properties.ETag;

                    // 412 if the blob has a different etag. 
                    access = AccessCondition.GenerateIfMatchCondition(etag);

                    obj = JsonConvert.DeserializeObject<TBlob>(oldJson);
                }
                catch (StorageException e)
                {
                    if (e.RequestInformation.HttpStatusCode == (int)HttpStatusCode.NotFound)
                    {
                        blob.Container.CreateIfNotExists();
                        obj = new TBlob();

                        // This will 409 if the blob already exists. 
                        access = AccessCondition.GenerateIfNoneMatchCondition("*");
                    }
                    else
                    {
                        throw;
                    }
                }

                TResult userValue = mutator(obj);
                string newJson = JsonConvert.SerializeObject(obj, Formatting.Indented);

                // Save back blob if it updated. 
                if (oldJson != newJson)
                {
                    try
                    {
                        blob.UploadText(newJson, accessCondition: access);
                    }
                    catch (StorageException e)
                    {
                        var code = (HttpStatusCode)e.RequestInformation.HttpStatusCode;
                        if (code == HttpStatusCode.PreconditionFailed || code == HttpStatusCode.Conflict)
                        {
                            // Could be a 412 or 409. 
                            // etag mismatch. Retry 
                            continue;
                        }

                        // Any other error is fatal.
                        throw;
                    }
                }

                return userValue;
            } // end retry 
        }

        public static string GetContainerName(string eventHubName, string eventHubNamespace, string storageConnectionString)
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(storageConnectionString);
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference(InternalContainerName);

            var blob = container.GetBlockBlobReference(InternalBlobPath);

            string key = eventHubNamespace + "/" + eventHubName;

            string name = MutateBlob<Dictionary<string, string>, string>(blob, (map) =>
            {
                string containerName;
                if (map.TryGetValue(key, out containerName))
                {
                    return containerName;
                }

               // Storage container, restrict to: [a-z0-9\-]{3,63}, no consecutive dashes, start&end with alpha
               // Guid is 32 chars long and ToString() yields lowercase.  
               string newName = "eventhubs-" + Guid.NewGuid().ToString("N");

               // Create a new container name for the event hub. 
               map[key] = newName;
                return newName;
            });

            return name;
        }
    }
}