// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using static Microsoft.Azure.EventHubs.EventData;

namespace Microsoft.Azure.WebJobs.EventHubs.UnitTests
{
    public class EventHubTests
    {
        [Fact]
        public void GetStaticBindingContract_ReturnsExpectedValue()
        {
            var strategy = new EventHubTriggerBindingStrategy();
            var contract = strategy.GetBindingContract();

            Assert.Equal(7, contract.Count);
            Assert.Equal(typeof(PartitionContext), contract["PartitionContext"]);
            Assert.Equal(typeof(string), contract["Offset"]);
            Assert.Equal(typeof(long), contract["SequenceNumber"]);
            Assert.Equal(typeof(DateTime), contract["EnqueuedTimeUtc"]);
            Assert.Equal(typeof(IDictionary<string, object>), contract["Properties"]);
            Assert.Equal(typeof(IDictionary<string, object>), contract["SystemProperties"]);
        }

        [Fact]
        public void GetBindingContract_SingleDispatch_ReturnsExpectedValue()
        {
            var strategy = new EventHubTriggerBindingStrategy();
            var contract = strategy.GetBindingContract(true);

            Assert.Equal(7, contract.Count);
            Assert.Equal(typeof(PartitionContext), contract["PartitionContext"]);
            Assert.Equal(typeof(string), contract["Offset"]);
            Assert.Equal(typeof(long), contract["SequenceNumber"]);
            Assert.Equal(typeof(DateTime), contract["EnqueuedTimeUtc"]);
            Assert.Equal(typeof(IDictionary<string, object>), contract["Properties"]);
            Assert.Equal(typeof(IDictionary<string, object>), contract["SystemProperties"]);
        }

        [Fact]
        public void GetBindingContract_MultipleDispatch_ReturnsExpectedValue()
        {
            var strategy = new EventHubTriggerBindingStrategy();
            var contract = strategy.GetBindingContract(false);

            Assert.Equal(7, contract.Count);
            Assert.Equal(typeof(PartitionContext), contract["PartitionContext"]);
            Assert.Equal(typeof(string[]), contract["PartitionKeyArray"]);
            Assert.Equal(typeof(string[]), contract["OffsetArray"]);
            Assert.Equal(typeof(long[]), contract["SequenceNumberArray"]);
            Assert.Equal(typeof(DateTime[]), contract["EnqueuedTimeUtcArray"]);
            Assert.Equal(typeof(IDictionary<string, object>[]), contract["PropertiesArray"]);
            Assert.Equal(typeof(IDictionary<string, object>[]), contract["SystemPropertiesArray"]);
        }

        [Fact]
        public void GetBindingData_SingleDispatch_ReturnsExpectedValue()
        {
            var evt = new EventData(new byte[] { });
            IDictionary<string, object> sysProps = GetSystemProperties();

            TestHelpers.SetField(evt, "SystemProperties", sysProps);

            var input = EventHubTriggerInput.New(evt);
            input.PartitionContext = GetPartitionContext();

            var strategy = new EventHubTriggerBindingStrategy();
            var bindingData = strategy.GetBindingData(input);

            Assert.Equal(7, bindingData.Count);
            Assert.Same(input.PartitionContext, bindingData["PartitionContext"]);
            Assert.Equal(evt.SystemProperties.PartitionKey, bindingData["PartitionKey"]);
            Assert.Equal(evt.SystemProperties.Offset, bindingData["Offset"]);
            Assert.Equal(evt.SystemProperties.SequenceNumber, bindingData["SequenceNumber"]);
            Assert.Equal(evt.SystemProperties.EnqueuedTimeUtc, bindingData["EnqueuedTimeUtc"]);
            Assert.Same(evt.Properties, bindingData["Properties"]);
            var bindingDataSysProps = bindingData["SystemProperties"] as IDictionary<string, object>;
            Assert.NotNull(bindingDataSysProps);
            Assert.True(CompareSystemProperties(bindingDataSysProps, sysProps));
        }

        private static IDictionary<string, object> GetSystemProperties(string partitionKey = "TestKey")
        {
            long testSequence = 4294967296;
            IDictionary<string, object> sysProps = TestHelpers.New<SystemPropertiesCollection>();
            sysProps["x-opt-partition-key"] = partitionKey;
            sysProps["x-opt-offset"] = "TestOffset";
            sysProps["x-opt-enqueued-time"] = DateTime.MinValue;
            sysProps["x-opt-sequence-number"] = testSequence;
            return sysProps;
        }

        private static bool CompareSystemProperties(IDictionary<string, object> actualProperties, IDictionary<string, object> expectedProperties)
        {
           return actualProperties.Keys.Count == expectedProperties.Keys.Count &&
                   actualProperties.Keys.All(k => expectedProperties.ContainsKey(k) && object.Equals(actualProperties[k], expectedProperties[k]));
        }

        [Fact]
        public void GetBindingData_MultipleDispatch_ReturnsExpectedValue()
        {

            var events = new EventData[3]
            {
                new EventData(Encoding.UTF8.GetBytes("Event 1")),
                new EventData(Encoding.UTF8.GetBytes("Event 2")),
                new EventData(Encoding.UTF8.GetBytes("Event 3")),
            };

            var count = 0;
            foreach (var evt in events)
            {
                IDictionary<string, object> sysProps = GetSystemProperties($"pk{count++}");
                TestHelpers.SetField(evt, "SystemProperties", sysProps);
            }

            var input = new EventHubTriggerInput
            {
                Events = events,
                PartitionContext = GetPartitionContext(),
            };
            var strategy = new EventHubTriggerBindingStrategy();
            var bindingData = strategy.GetBindingData(input);

            Assert.Equal(7, bindingData.Count);
            Assert.Same(input.PartitionContext, bindingData["PartitionContext"]);

            // verify an array was created for each binding data type
            Assert.Equal(events.Length, ((string[])bindingData["PartitionKeyArray"]).Length);
            Assert.Equal(events.Length, ((string[])bindingData["OffsetArray"]).Length);
            Assert.Equal(events.Length, ((long[])bindingData["SequenceNumberArray"]).Length);
            Assert.Equal(events.Length, ((DateTime[])bindingData["EnqueuedTimeUtcArray"]).Length);
            Assert.Equal(events.Length, ((IDictionary<string, object>[])bindingData["PropertiesArray"]).Length);
            Assert.Equal(events.Length, ((IDictionary<string, object>[])bindingData["SystemPropertiesArray"]).Length);

            Assert.Equal(events[0].SystemProperties.PartitionKey, ((string[])bindingData["PartitionKeyArray"])[0]);
            Assert.Equal(events[1].SystemProperties.PartitionKey, ((string[])bindingData["PartitionKeyArray"])[1]);
            Assert.Equal(events[2].SystemProperties.PartitionKey, ((string[])bindingData["PartitionKeyArray"])[2]);
        }

        [Fact]
        public void TriggerStrategy()
        {
            string data = "123";

            var strategy = new EventHubTriggerBindingStrategy();
            EventHubTriggerInput triggerInput = strategy.ConvertFromString(data);

            var contract = strategy.GetBindingData(triggerInput);

            EventData single = strategy.BindSingle(triggerInput, null);
            string body = Encoding.UTF8.GetString(single.Body.Array);

            Assert.Equal(data, body);
            Assert.Null(contract["PartitionContext"]);
            Assert.Null(contract["partitioncontext"]); // case insensitive
        }

        // Validate that if connection string has EntityPath, that takes precedence over the parameter.
        [Theory]
        [InlineData("k1", "Endpoint=sb://test89123-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey")]
        [InlineData("path2", "Endpoint=sb://test89123-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey;EntityPath=path2")]
        public void EntityPathInConnectionString(string expectedPathName, string connectionString)
        {
            EventHubConfiguration config = new EventHubConfiguration();

            // Test sender
            config.AddSender("k1", connectionString);
            var client = config.GetEventHubClient("k1", null);
            Assert.Equal(expectedPathName, client.EventHubName);
        }

        // Validate that if connection string has EntityPath, that takes precedence over the parameter.
        [Theory]
        [InlineData("k1", "Endpoint=sb://test89123-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey")]
        [InlineData("path2", "Endpoint=sb://test89123-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey;EntityPath=path2")]
        public void GetEventHubClient_AddsConnection(string expectedPathName, string connectionString)
        {
            EventHubConfiguration config = new EventHubConfiguration();
            var client = config.GetEventHubClient("k1", connectionString);
            Assert.Equal(expectedPathName, client.EventHubName);
        }

        [Theory]
        [InlineData("e", "n1", "n1/e/")]
        [InlineData("e--1", "host_.path.foo", "host_.path.foo/e--1/")]
        [InlineData("Ab", "Cd", "cd/ab/")]
        [InlineData("A=", "Cd", "cd/a:3D/")]
        [InlineData("A:", "Cd", "cd/a:3A/")]
        public void EventHubBlobPrefix(string eventHubName, string serviceBusNamespace, string expected)
        {
            string actual = EventHubConfiguration.GetBlobPrefix(eventHubName, serviceBusNamespace);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(200)]
        public void EventHubBatchCheckpointFrequency(int num)
        {
            var config = new EventHubConfiguration();
            config.BatchCheckpointFrequency = num;
            Assert.Equal(num, config.BatchCheckpointFrequency);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public void EventHubBatchCheckpointFrequency_Throws(int num)
        {
            var config = new EventHubConfiguration();
            Assert.Throws<InvalidOperationException>(() => config.BatchCheckpointFrequency = num);
        }

        [Fact]
        public void InitializeFromHostMetadata()
        {
            // TODO: It's tough to wire all this up without using a new host.
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost(builder =>
                {
                    builder.AddEventHubs();
                })
                .ConfigureAppConfiguration(c =>
                {
                    c.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "AzureWebJobs:extensions:EventHubs:MaxBatchSize", "100" },
                        { "AzureWebJobs:extensions:EventHubs:PrefetchCount", "200" },
                        { "AzureWebJobs:extensions:EventHubs:BatchCheckpointFrequency", "5" },
                    });
                })
                .Build();

            // Force the ExtensionRegistryFactory to run, which will initialize the EventHubConfiguration.
            var extensionRegistry = host.Services.GetService<IExtensionRegistry>();
            var eventHubConfig = host.Services.GetService<EventHubConfiguration>();

            var options = eventHubConfig.GetOptions();
            Assert.Equal(100, options.MaxBatchSize);
            Assert.Equal(200, options.PrefetchCount);
            Assert.Equal(5, eventHubConfig.BatchCheckpointFrequency);
        }

        internal static PartitionContext GetPartitionContext()
        {
            var constructor = typeof(PartitionContext).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new Type[] { typeof(EventProcessorHost), typeof(string), typeof(string), typeof(string), typeof(CancellationToken) },
                null);
            return (PartitionContext)constructor.Invoke(new object[] { null, null, null, null, null });
        }
    }
}
