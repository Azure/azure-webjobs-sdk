// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    /// <summary>
    /// Provide configuration for event hubs. 
    /// This is primarily mapping names to underlying EventHub listener and receiver objects from the EventHubs SDK. 
    /// </summary>
    public class ServiceBusExtensions : ExtensionBase
    {
        private EventHubConfiguration _eventHubConfiguration;

        /// <inheritdoc/>       
        public ServiceBusExtensions()
        {
            this.ResolvedAssemblies = new Assembly[]
            {
                typeof(BrokeredMessage).Assembly
            };
        }

        /// <inheritdoc/>       
        protected override IEnumerable<Type> ExposedAttributes
        {
            get
            {
                return new Type[]
                {
                    typeof(EventHubAttribute),
                    typeof(EventHubTriggerAttribute)
                };
            }
        }

        /// <inheritdoc/>       
        public override Task InitAsync(JobHostConfiguration config, JObject metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException("metadata");
            }
            var settings = metadata.ToObject<Settings>();

            EventProcessorOptions eventProcessorOptions = EventProcessorOptions.DefaultOptions;
            eventProcessorOptions.MaxBatchSize = 1000;

            ServiceBusConfiguration serviceBusConfig = new ServiceBusConfiguration();

            if (settings != null)
            {
                if (settings.ServiceBus != null)
                {
                    settings.ServiceBus.Apply(serviceBusConfig);
                }
                if (settings.EventHub != null)
                {
                    settings.EventHub.Apply(eventProcessorOptions);
                }
            }

            _eventHubConfiguration = new EventHubConfiguration(eventProcessorOptions);
            config.UseEventHub(_eventHubConfiguration);
            config.UseServiceBus(serviceBusConfig);

            return Task.FromResult(0);
        }

        /// <inheritdoc/>       
        public override Attribute[] GetAttributes(Type attributeType, JObject metadata)
        {
            var attributes = base.GetAttributes(attributeType, metadata);
            if (attributeType == typeof(EventHubTriggerAttribute))
            {
                // $$$ Can we get rid of this? 
                var attr = (EventHubTriggerAttribute)attributes[0];
                this._eventHubConfiguration.AddReceiver(attr.EventHubName, attr.Connection);
            }
            return attributes;
        }

        /// <inheritdoc/>       
        public override Type GetDefaultType(FileAccess access, Cardinality cardinality, DataType dataType, Attribute attribute)
        {
            if (attribute is EventHubTriggerAttribute)
            {
                // $$$ Trigger isn't rule-based. 
                if (access == FileAccess.Read)
                {
                    var type = (dataType == DataType.Binary) ? typeof(byte[]) : typeof(string);
                    if (cardinality == Cardinality.Many)
                    {
                        type = type.MakeArrayType();
                    }
                    return type;
                }
            }
            
            return base.GetDefaultType(access, cardinality, dataType, attribute);
        }

        // Options for self-configuration. 
        private class Settings
        {
            public EventHubConfigOptions EventHub { get; set; }
            public ServiceBusConfigOptions ServiceBus { get; set; }

            public class EventHubConfigOptions
            {
                public int? MaxBatchSize { get; set; }
                public int? PrefetchCount { get; set; }

                public void Apply(EventProcessorOptions eventProcessorOptions)
                {
                    if (this.MaxBatchSize.HasValue)
                    {
                        eventProcessorOptions.MaxBatchSize = this.MaxBatchSize.Value;
                    }
                    if (this.PrefetchCount.HasValue)
                    {
                        eventProcessorOptions.PrefetchCount = this.PrefetchCount.Value;
                    }
                }
            }

            public class ServiceBusConfigOptions
            {
                public int? MaxConcurrentCalls { get; set; }
                public int? PrefetchCount { get; set; }

                public void Apply(ServiceBusConfiguration config)
                {
                    if (this.MaxConcurrentCalls.HasValue)
                    {
                        config.MessageOptions.MaxConcurrentCalls = this.MaxConcurrentCalls.Value;
                    }

                    if (this.PrefetchCount.HasValue)
                    {
                        config.PrefetchCount = this.PrefetchCount.Value;
                    }
                }
            }
        }
    }
}