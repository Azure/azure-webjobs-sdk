// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SampleHost.Filters;

namespace SampleHost
{
    [StorageAccount("%storageAccount%")]
    [ErrorHandler]
    public class Functions
    {
        public void ProcessQueueMessage(
            [BlobTrigger("%containerName%/{name}")] CloudBlockBlob blob,
            string name,
            IDictionary<string, string> metadata,
            Uri uri,
            BlobProperties properties,
            [Queue("%queueName%")] ICollector<string> outputQueue,
            ILogger logger)
        {
            logger.LogInformation("Reading {name}.");

            if (!metadata.ContainsKey("OperationId"))
            {
                logger.LogError("Missing OperationId in metadata. Skip putting the message to queue.");
            }
            else
            {
                // Copy metadata to a separate dictionary for output. 
                // It must contains the required metadata OperationId, 
                // and may contains the optional metadata Fileuri.
                var outputDict = new Dictionary<string, string>(metadata)
                {
                    { "Timestamp", $"{properties.Created:o}" }
                };
                if (!outputDict.ContainsKey("Fileuri"))
                {
                    // Add Fileuri if not provided
                    outputDict.Add("Fileuri", uri.ToString());
                }

                // Convert to Json string and put onto the queue
                string outputJson = JsonConvert.SerializeObject(outputDict);
                logger.LogInformation($" Metadata: {outputJson}");
                outputQueue.Add(outputJson);
            }
        }
    }
}
