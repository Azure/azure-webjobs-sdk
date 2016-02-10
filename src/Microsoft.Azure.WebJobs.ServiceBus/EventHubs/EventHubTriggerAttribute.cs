// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    // Setup an 'trigger' to listen on events from an event hub. 
    public class EventHubTriggerAttribute : Attribute
    {
        public string EventHubName { get; private set; }

        public EventHubTriggerAttribute(string eventHubName)
        {
            this.EventHubName = eventHubName;
        }
    }
}