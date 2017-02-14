// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    /// <summary>
    /// Setup an 'trigger' on a parameter to listen on events from an event hub. 
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1019:DefineAccessorsForAttributeArguments")]
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class EventHubTriggerAttribute : Attribute
    {
        /// <summary>
        /// Create an instance of this attribute.
        /// </summary>
        /// <param name="eventHubName">Event hub to listen on for messages. </param>
        public EventHubTriggerAttribute(string eventHubName)
        {
            this.EventHubName = eventHubName;
        }

        /// <summary>
        /// Name of the event hub. 
        /// </summary>
        [AutoResolve]
        public string EventHubName { get; private set; }

        /// <summary>
        /// Optional Name of the consumer group. If missing, then use the default name, "$Default"
        /// </summary>
        public string ConsumerGroup { get; set; }

        /// <summary>
        /// Optional - the app setting for the the connection string.
        /// If missing, then get this from the EventHubConfiguration. 
        /// </summary>
        [AutoResolve(AllowTokens = false)]
        public string Connection { get; set; }

        private class EventHubTriggerAttributeMetadata : AttributeMetadata
        {
            public string Path { get; set; }
            public string ConsumerGroup { get; set; }
            public string Connection { get; set; }

            public override Attribute GetAttribute()
            {
                return new EventHubTriggerAttribute(Path) { ConsumerGroup = ConsumerGroup, Connection = Connection };
            }
        }
    }
}