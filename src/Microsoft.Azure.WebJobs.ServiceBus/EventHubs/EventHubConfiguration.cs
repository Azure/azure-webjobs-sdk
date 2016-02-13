// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.ServiceBus.Messaging;
using Microsoft.Azure.WebJobs.Host.Protocols;
using System.Globalization;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.ServiceBus
{
    public class EventHubConfiguration : IExtensionConfigProvider, IEventHubProvider
    {
        // $$$ are names case-sensitive?
        Dictionary<string, EventHubClient> _senders = new Dictionary<string, EventHubClient>();
        Dictionary<string, EventProcessorHost> _listeners  = new Dictionary<string, EventProcessorHost>();

        private List<Action<JobHostConfiguration>> _deferredWork = new List<Action<JobHostConfiguration>>();

        public void AddSender(EventHubClient client)
        {
            string name = client.Path;
            _senders[name] = client;
        }

        public void AddSender(string eventHubName, string sendConnectionString)
        {
            var client = EventHubClient.CreateFromConnectionString(sendConnectionString, eventHubName);
            AddSender(client);
        }

        
        private void AddReceiver(string eventHubName, EventProcessorHost listener)
        {
            _listeners[eventHubName] = listener;
        }

        public void AddReceiver(string eventHubName, string receiverConnectionString)
        {
            // We can't get the storage string until we get a JobHostConfig. So defer this. 
            _deferredWork.Add((jobHostConfig) =>
           {
               string storageConnectionString = jobHostConfig.StorageConnectionString;
               this.AddReceiver(eventHubName, receiverConnectionString, storageConnectionString);
           });
        }

        public void AddReceiver(string eventHubName, string receiverConnectionString, string storageConnectionString)
        {
            string eventProcessorHostName = Guid.NewGuid().ToString();

            EventProcessorHost eventProcessorHost = new EventProcessorHost(
                eventProcessorHostName, 
                eventHubName, 
                EventHubConsumerGroup.DefaultGroupName,
                receiverConnectionString, 
                storageConnectionString);

            this.AddReceiver(eventHubName, eventProcessorHost);
        }


        EventHubClient IEventHubProvider.GetEventHubClient(string eventHubName)
        {
            EventHubClient client;
            if (_senders.TryGetValue(eventHubName, out client))             
            {
                return client;
            }
            throw new InvalidOperationException("No EventHubClient (sender) named " + eventHubName);
        }

        EventProcessorHost IEventHubProvider.GetEventProcessorHost(string eventHubName)
        {
            EventProcessorHost host;
            if (_listeners.TryGetValue(eventHubName, out host))
            {
                return host;
            }
            throw new InvalidOperationException("No EventProcessorHost (receiver) named " + eventHubName);
        }

        EventProcessorOptions IEventHubProvider.GetOptions()
        {
            EventProcessorOptions options = new EventProcessorOptions
            {
                MaxBatchSize = 1000
            };
            return options;
        }

        void IExtensionConfigProvider.Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            // Deferred list 
            foreach (var action in _deferredWork)
            {
                action(context.Config);
            }
            _deferredWork.Clear();

            // get the services we need to construct our binding providers
            INameResolver nameResolver = context.Config.NameResolver;
            IExtensionRegistry extensions = context.Config.GetService<IExtensionRegistry>();

            IConverterManager cm = context.Config.GetOrCreateConverterManager();
            cm.AddConverter<string, EventData>(ConvertString2EventData); 
            cm.AddConverter<byte[], EventData>(ConvertBytes2EventData); // direct, handles non-string representations

            // register our trigger binding provider
            var triggerBindingProvider = new EventHubTriggerAttributeBindingProvider(nameResolver, this);
            extensions.RegisterExtension<ITriggerBindingProvider>(triggerBindingProvider);

            // register our binding provider
            var bindingProvider = new EventHubAttributeBindingProvider(nameResolver, this, cm);
            extensions.RegisterExtension<IBindingProvider>(bindingProvider);
        }

        private static EventData ConvertBytes2EventData(byte[] input)
        {
            var eventData = new EventData(input);
            return eventData;
        }

        private static EventData ConvertString2EventData(string input)
        {
            var eventData = new EventData(Encoding.UTF8.GetBytes(input));
            return eventData;
        }
    }

}
