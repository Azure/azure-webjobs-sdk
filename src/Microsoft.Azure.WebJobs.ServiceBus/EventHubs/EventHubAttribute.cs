// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    /// <summary>
    /// Setup an 'output' binding to an EventHub. This can be any output type compatible with an IAsyncCollector.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1019:DefineAccessorsForAttributeArguments")]
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class EventHubAttribute : Attribute
    {
        /// <summary>
        /// Initialize a new instance of the <see cref="EventHubAttribute"/>
        /// </summary>
        /// <param name="eventHubName">Name of the event hub as resolved against the <see cref="EventHubConfiguration"/> </param>
        public EventHubAttribute(string eventHubName)
        {
            this.EventHubName = eventHubName;
        }

        /// <summary>
        /// The name of the event hub. This is resolved against the <see cref="EventHubConfiguration"/>
        /// </summary>
        [AutoResolve]
        public string EventHubName { get; private set; }
        
        /// <summary>
        /// Optional - the app setting for the the connection string.
        /// If missing, then get this from the EventHubConfiguration. 
        /// </summary>
        [AutoResolve(AllowTokens = false)]
        public string Connection { get; set; }

        private class EventHubTriggerAttributeMetadata : AttributeMetadata
        {
            public string Path { get; set; }
            public string Connection { get; set; }

            public override Attribute GetAttribute()
            {
                return new EventHubAttribute(Path) { Connection = Connection };
            }
        }
    }    
}