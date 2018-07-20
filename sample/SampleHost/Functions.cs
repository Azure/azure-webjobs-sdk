// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SampleHost.Filters;
using SampleHost.Models;

namespace SampleHost
{
    [ErrorHandler]
    public static class Functions
    {
        [Singleton]
        public static void BlobTrigger(
            [BlobTrigger("test")] string blob, ILogger logger)
        {
            logger.LogInformation("Processed blob: " + blob);
        }

        public static void BlobPoisonBlobHandler(
            [QueueTrigger("webjobs-blobtrigger-poison")] JObject blobInfo, ILogger logger)
        {
            string container = (string)blobInfo["ContainerName"];
            string blobName = (string)blobInfo["BlobName"];

            logger.LogInformation($"Poison blob: {container}/{blobName}");
        }

        [WorkItemValidator]
        public static void ProcessWorkItem(
            [QueueTrigger("test")] WorkItem workItem, ILogger logger)
        {
            logger.LogInformation($"Processed work item {workItem.ID}");
        }

        public static async Task ProcessWorkItem_ServiceBus(
            [ServiceBusTrigger("test-items")] WorkItem item,
            string messageId,
            int deliveryCount,
            ILogger log)
        {
            log.LogInformation($"Processing ServiceBus message (Id={messageId}, DeliveryCount={deliveryCount})");

            await Task.Delay(1000);

            log.LogInformation($"Message complete (Id={messageId})");
        }

        public static void ProcessEvents([EventHubTrigger("testhub2", Connection = "TestEventHubConnection")] EventData[] events,
            ILogger log)
        {
            foreach (var evt in events)
            {
                log.LogInformation($"Event processed (Offset={evt.SystemProperties.Offset}, SequenceNumber={evt.SystemProperties.SequenceNumber})");
            }
        }
    }
}
