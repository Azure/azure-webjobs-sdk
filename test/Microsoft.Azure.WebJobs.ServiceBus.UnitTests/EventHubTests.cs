using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ServiceBus.Messaging;
using Xunit;
using Microsoft.Azure.WebJobs.Host.Config;
using Newtonsoft.Json.Linq;
using Microsoft.ServiceBus;
using System.Linq;
using System.Reflection;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests
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
            var evt = new EventData();
            evt.PartitionKey = "TestKey";
            var input = EventHubTriggerInput.New(evt);
            input.PartitionContext = new PartitionContext();

            var strategy = new EventHubTriggerBindingStrategy();
            var bindingData = strategy.GetBindingData(input);

            Assert.Equal(7, bindingData.Count);
            Assert.Same(input.PartitionContext, bindingData["PartitionContext"]);
            Assert.Equal(evt.PartitionKey, bindingData["PartitionKey"]);
            Assert.Equal(evt.Offset, bindingData["Offset"]);
            Assert.Equal(evt.SequenceNumber, bindingData["SequenceNumber"]);
            Assert.Equal(evt.EnqueuedTimeUtc, bindingData["EnqueuedTimeUtc"]);
            Assert.Same(evt.Properties, bindingData["Properties"]);
            Assert.Same(evt.SystemProperties, bindingData["SystemProperties"]);
        }

        [Fact]
        public void GetBindingData_MultipleDispatch_ReturnsExpectedValue()
        {
            var events = new EventData[3]
            {
                new EventData(Encoding.UTF8.GetBytes("Event 1"))
                {
                    PartitionKey = "pk1"
                },
                new EventData(Encoding.UTF8.GetBytes("Event 2"))
                {
                    PartitionKey = "pk2"
                },
                new EventData(Encoding.UTF8.GetBytes("Event 3"))
                {
                    PartitionKey = "pk3"
                },
            };

            var input = new EventHubTriggerInput
            {
                PartitionContext = new PartitionContext(),
                Events = events
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

            // verify event values are distributed to arrays properly
            Assert.Equal(events[0].PartitionKey, ((string[])bindingData["PartitionKeyArray"])[0]);
            Assert.Equal(events[1].PartitionKey, ((string[])bindingData["PartitionKeyArray"])[1]);
            Assert.Equal(events[2].PartitionKey, ((string[])bindingData["PartitionKeyArray"])[2]);
        }

        [Fact]
        public void TriggerStrategy()
        {
            string data = "123";

            var strategy = new EventHubTriggerBindingStrategy();
            EventHubTriggerInput triggerInput = strategy.ConvertFromString(data);

            var contract = strategy.GetBindingData(triggerInput);

            EventData single = strategy.BindSingle(triggerInput, null);
            string body = Encoding.UTF8.GetString(single.GetBytes());

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
            var client = config.GetEventHubClient("k1", connectionString);
            Assert.Equal(expectedPathName, client.Path);
        }

        private class TestNameResolver : INameResolver
        {
            public IDictionary<string, string> env = new Dictionary<string, string>();

            public string Resolve(string name) => env[name];
        }

        // Validate that if connection string has EntityPath, that takes precedence over the parameter. 
        [Theory]
        [InlineData("k1", "Endpoint=sb://test89123-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey")]
        [InlineData("path2", "Endpoint=sb://test89123-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey;EntityPath=path2")]
        public void GetEventHubClient_AddsConnection(string expectedPathName, string connectionString)
        {
            EventHubConfiguration config = new EventHubConfiguration();
            var client = config.GetEventHubClient("k1", connectionString);
            Assert.Equal(expectedPathName, client.Path);
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
        public void UseReflectionToAccessEndpointFromEventHubClient()
        {
            var connectionString = "Endpoint=sb://test89123-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey";
            var client = EventHubClient.CreateFromConnectionString(connectionString, "hub1");
            var endpoint = EventHubConfiguration.GetEndpointFromEventHubClient(client);

            var connectionBuilder = new ServiceBusConnectionStringBuilder(connectionString);
            Assert.Equal(connectionBuilder.Endpoints.Single(), endpoint);
        }

        [Theory]
        [InlineData("myhub", "myhub", "myhub")]
        [InlineData("myhub", "MYHUB", "myhub")]
        [InlineData("myhub", "myhub", "MYHUB")]
        public void AddEventHubClient_EventHubNamesAreCaseInsensitive(string hubNameCreate, string hubNameAdd, string hubNameGet)
        {
            string connectionString = "Endpoint=sb://test1-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey";

            EventHubConfiguration config = new EventHubConfiguration();

            var client = EventHubClient.CreateFromConnectionString(connectionString, hubNameCreate);
            config.AddEventHubClient(hubNameAdd, client);

            var retrieval = config.GetEventHubClient(hubNameGet, connectionString);
            Assert.Same(client, retrieval);
        }

        [Fact]
        public void AddEventHubClient_SupportsTwoEventHubsWithSameNameInDifferentNamespaces()
        {
            string connectionString1 = "Endpoint=sb://test1-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey";
            string connectionString2 = "Endpoint=sb://test2-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey";

            var client1 = EventHubClient.CreateFromConnectionString(connectionString1, "myhub");
            var client2 = EventHubClient.CreateFromConnectionString(connectionString2, "myhub");

            EventHubConfiguration config = new EventHubConfiguration();
            config.AddEventHubClient(client1);
            config.AddEventHubClient(client2);

            EventHubClient namespace1Retrieval1 = config.GetEventHubClient("myhub", connectionString1);
            EventHubClient namespace2Retrieval1 = config.GetEventHubClient("myhub", connectionString2);            
            
            Assert.Equal(client1, namespace1Retrieval1);
            Assert.Equal(client2, namespace2Retrieval1);
        }

        [Fact]
        public void AddEventHubClient_HasOverwriteSemantics()
        {
            // Note: this test was written to verify preservation of existing behavior. Unclear whether overwrite semantics are actually
            // desirable or a simple side-effect of the way the code was written

            string connectionString = "Endpoint=sb://test1-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey";

            var client1 = EventHubClient.CreateFromConnectionString(connectionString, "myhub");
            var client2 = EventHubClient.CreateFromConnectionString(connectionString, "myhub");

            EventHubConfiguration config = new EventHubConfiguration();
            config.AddEventHubClient(client1);
            config.AddEventHubClient(client2);

            var retrievedClient = config.GetEventHubClient("myhub", connectionString);

            // Last one wins
            Assert.Same(client2, retrievedClient);
        }

        [Fact]
        public void AddEventHubClient_AllowsPassedEventHubNameToActAsAUniqueKey()
        {
            // Note: this test is verifying behavior that is not desirable long term but must be preserved in this major version to avoid breaking changes

            string connectionString = "Endpoint=sb://test1-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey";

            var client1 = EventHubClient.CreateFromConnectionString(connectionString, "myhub");
            var client2 = EventHubClient.CreateFromConnectionString(connectionString, "myhub");

            EventHubConfiguration config = new EventHubConfiguration();
            config.AddEventHubClient("hub1", client1);
            config.AddEventHubClient("hub2", client2);

            var client1Retrieval1 = config.GetEventHubClient("hub1", connectionString);
            var client2Retrieval1 = config.GetEventHubClient("hub2", connectionString);

            // These are two different instances
            Assert.NotSame(client1Retrieval1, client2Retrieval1);
        }

        [Fact]
        public void GetEventHubClient_DoesNotRequireConnection_IfMatchingSenderWasAdded()
        {
            // Note: this test is verifying behavior that is not desirable long term but must be preserved in this major version to avoid breaking changes

            string connectionString = "Endpoint=sb://test1-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey";

            EventHubConfiguration config = new EventHubConfiguration();

            config.AddSender("myHub", connectionString);
            var client = config.GetEventHubClient("myhub", null);

            Assert.NotNull(client);
        }

        [Fact]
        public void CanAddBothReceiverAndSenderForSameHub()
        {
            // Note: this test is verifying behavior that is not desirable long term but must be preserved in this major version to avoid breaking changes

            string connectionString = "Endpoint=sb://test1-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey";
            string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=GTfc0SIUd4Mg6n8wBzCRy29wnx2VVd+90MQKnJtgoCj4rB8LAIQ==";

            EventHubConfiguration config = new EventHubConfiguration();
            config.DefaultStorageString = storageConnectionString;

            config.AddSender("myHub", connectionString);
            config.AddReceiver("myHub", connectionString);

            var client = config.GetEventHubClient("myhub", null);
            var host = config.GetEventProcessorHost("myhub", EventHubConsumerGroup.DefaultGroupName);

            Assert.NotNull(client);
            Assert.NotNull(host);
        }

        [Fact]
        public void AddSender_HasOverwriteSemantics()
        {
            // Note: this test was written to verify preservation of existing behavior. Unclear whether overwrite semantics are actually
            // desirable or a simple side-effect of the way the code was written

            string connectionString = "Endpoint=sb://test1-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey";

            EventHubConfiguration config = new EventHubConfiguration();

            config.AddSender("myHub", connectionString);
            var retrieval1 = config.GetEventHubClient("myhub", connectionString);

            config.AddSender("myHub", connectionString);
            var retrieval2 = config.GetEventHubClient("myhub", connectionString);

            // These are two different instances
            Assert.NotSame(retrieval1, retrieval2);
        }

        [Fact]
        public void AddReceiver_AllowsForAnEventProcessorHostToBeRetrieved()
        {
            string connectionString = "Endpoint=sb://test1-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey";
            string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=GTfc0SIUd4Mg6n8wBzCRy29wnx2VVd+90MQKnJtgoCj4rB8LAIQ==";

            EventHubConfiguration config = new EventHubConfiguration();
            config.DefaultStorageString = storageConnectionString;
            config.AddReceiver("foo", connectionString);

            var host = config.GetEventProcessorHost("foo", EventHubConsumerGroup.DefaultGroupName);
            Assert.NotNull(host);
        }

        [Fact]
        public void AddReceiver_SupportsTwoEventHubsWithSameNameInDifferentNamespaces()
        {
            string connectionString1 = "Endpoint=sb://test1-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey";
            string connectionString2 = "Endpoint=sb://test2-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey";

            string storageConnectionString1 = "DefaultEndpointsProtocol=https;AccountName=myaccount1;AccountKey=GTfc0SIUd4Mg6n8wBzCRy29wnx2VVd+90MQKnJtgoCj4rB8LAIQ==";
            string storageConnectionString2 = "DefaultEndpointsProtocol=https;AccountName=myaccount2;AccountKey=GTfc0SIUd4Mg6n8wBzCRy29wnx2VVd+90MQKnJtgoCj4rB8LAIQ==";

            EventHubConfiguration config = new EventHubConfiguration();

            config.AddReceiver("foo", connectionString1, storageConnectionString1);
            config.AddReceiver("foo", connectionString2, storageConnectionString2);

            var host1 = config.GetEventProcessorHost("foo", EventHubConsumerGroup.DefaultGroupName, connectionString1);
            var host2 = config.GetEventProcessorHost("foo", EventHubConsumerGroup.DefaultGroupName, connectionString2);

            // Verify that the correct storage connection string was used for each host
            // Since the EventProcessorHost type has basically zero public members we need to use reflection
            var blobClientProperty = typeof(EventProcessorHost).GetProperty("CloudBlobClient", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(blobClientProperty);

            var host1Client = (CloudBlobClient)blobClientProperty.GetValue(host1);
            var host2Client = (CloudBlobClient)blobClientProperty.GetValue(host2);

            Assert.Contains("myaccount1", host1Client.BaseUri.ToString());
            Assert.Contains("myaccount2", host2Client.BaseUri.ToString());            
        }

        [Fact]
        public void GetEventHubClient_ForSameHubNameAndConnectionString_ReturnsTheSameInstanceEachTime()
        {
            string connectionString = "Endpoint=sb://test1-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey";

            EventHubConfiguration config = new EventHubConfiguration();

            var retrieval1 = config.GetEventHubClient("myhub", connectionString);
            var retrieval2 = config.GetEventHubClient("myhub", connectionString);

            Assert.Same(retrieval1, retrieval2);
        }

        [Fact]
        public void GetEventHubClient_SupportsTwoEventHubsWithSameNameInDifferentNamespaces()
        {
            string connectionString1 = "Endpoint=sb://test1-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey";
            string connectionString2 = "Endpoint=sb://test2-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey";

            EventHubConfiguration config = new EventHubConfiguration();
            EventHubClient namespace1Retrieval1 = config.GetEventHubClient("myhub", connectionString1);
            EventHubClient namespace2Retrieval1 = config.GetEventHubClient("myhub", connectionString2);

            // These are different clients
            Assert.NotSame(namespace1Retrieval1, namespace2Retrieval1);

            EventHubClient namespace1Retrieval2 = config.GetEventHubClient("myhub", connectionString1);
            EventHubClient namespace2Retrieval2 = config.GetEventHubClient("myhub", connectionString2);

            // Again, two different clients
            Assert.NotSame(namespace1Retrieval2, namespace2Retrieval2);

            // But we only have two different clients here, so these pairs should match
            Assert.Same(namespace1Retrieval1, namespace1Retrieval2);
            Assert.Same(namespace2Retrieval1, namespace2Retrieval2);
        }

        [Fact]
        public void GetEventHubClient_HubInConnectionStringTakesPriority()
        {
            string connectionString1 = "Endpoint=sb://test1-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey;EntityPath=hub1";
            string connectionString2 = "Endpoint=sb://test2-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey;EntityPath=hub1";

            EventHubConfiguration config = new EventHubConfiguration();
            EventHubClient client1 = config.GetEventHubClient("myhub", connectionString1);
            EventHubClient client2 = config.GetEventHubClient("myhub", connectionString2);

            Assert.Equal("hub1", client1.Path);
            Assert.Equal("hub1", client2.Path);

            Assert.NotSame(client1, client2);
        }

        [Fact]
        public void AddEventProcessorHost_AllowsAnExplicitHostToBeProvidedAndRetrieved()
        {
            // Note: this test is verifying behavior that is not desirable long term but must be preserved in this major version to avoid breaking changes

            string connectionString = "Endpoint=sb://test1-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey";
            string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=GTfc0SIUd4Mg6n8wBzCRy29wnx2VVd+90MQKnJtgoCj4rB8LAIQ==";

            EventHubConfiguration config = new EventHubConfiguration();

            var host = new EventProcessorHost("myhub", EventHubConsumerGroup.DefaultGroupName, connectionString, storageConnectionString);
            config.AddEventProcessorHost("myhub", host);

            var retrieval1 = config.GetEventProcessorHost("myhub", null);
            Assert.Same(host, retrieval1);
        }

        [Fact]
        public void AddEventProcessorHost_AllowsPassedEventHubNameToActAsAUniqueKey()
        {
            // Note: this test is verifying behavior that is not desirable long term but must be preserved in this major version to avoid breaking changes

            string connectionString = "Endpoint=sb://test1-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey";
            string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=GTfc0SIUd4Mg6n8wBzCRy29wnx2VVd+90MQKnJtgoCj4rB8LAIQ==";

            EventHubConfiguration config = new EventHubConfiguration();

            var host = new EventProcessorHost("myhub", EventHubConsumerGroup.DefaultGroupName, connectionString, storageConnectionString);
            config.AddEventProcessorHost("foo", host);

            var retrieval1 = config.GetEventProcessorHost("foo", null);
            Assert.Same(host, retrieval1);
        }

        [Fact]
        public void InitializeFromHostMetadata()
        {
            var config = new EventHubConfiguration();
            var context = new ExtensionConfigContext()
            {
                Config = new JobHostConfiguration()
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    HostConfigMetadata = new JObject
#pragma warning restore CS0618 // Type or member is obsolete
                    {
                        {
                            "EventHub", new JObject {
                                { "MaxBatchSize", 100 },
                                { "PrefetchCount", 200 },
                                { "BatchCheckpointFrequency", 5 }
                            }
                        }
                    }
                }
            };

            (config as IExtensionConfigProvider).Initialize(context);

            var options = config.GetOptions();
            Assert.Equal(options.MaxBatchSize, 100);
            Assert.Equal(options.PrefetchCount, 200);
            Assert.Equal(config.BatchCheckpointFrequency, 5);
        }
    }
}
