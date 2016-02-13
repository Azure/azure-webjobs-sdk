// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.ServiceBus.Messaging;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    // Expose to binders so they can get the EventHub connections. 
    interface IEventHubProvider
    {
        EventHubClient GetEventHubClient(string eventHubName);

        EventProcessorHost GetEventProcessorHost(string eventHubName);

        EventProcessorOptions GetOptions();
    }

}