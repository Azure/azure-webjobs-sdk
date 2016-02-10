// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    // Setup an 'output' binding to an IAsyncCollector<EventData> compatible type.
    public class EventHubAttribute : Attribute
    {
        public string EventHubName { get; private set; }

        public EventHubAttribute(string eventHubName)
        {
            this.EventHubName = eventHubName;
        }
    }    
}