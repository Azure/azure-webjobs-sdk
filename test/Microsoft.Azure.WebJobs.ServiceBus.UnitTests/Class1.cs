// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests
{
    public class EventHubToolingTests // $$$
    {
        class Empty { }

        JobHostConfiguration GetConfig()
        {
            var appSettings = new FakeNameResolver();
            appSettings.Add("MyConnection", "Endpoint=sb://test89123-ns-x.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=secretkey");
            appSettings.Add("foo", "mypath");
            var config = TestHelpers.NewConfig(typeof(Empty), appSettings);
            return config;
        }

        // Checks that we write the marker file when we call the host
        [Fact]
        public async Task EventHubOutput()
        {
            var config = GetConfig();

            // JObject from Function.Json
            var functionMetadata = JObject.Parse(@"
    {
        'type': 'eventHub',
        'name': 'output',
        'direction': 'out',
        'connection': 'MyConnection',
        'path' : '%foo%'
    }");

            ToolingHelper tooling = new ToolingHelper(config);

            // Pull in the extension
            JObject hostMetadata = null;
            await tooling.AddAssemblyAsync(typeof(EventHubConfiguration).Assembly, hostMetadata);
            await tooling.FinishAddsAsync();

            string name = functionMetadata["type"].ToString();
            var attrType = tooling.GetAttributeTypeFromName(name);
            Assert.Equal(typeof(EventHubAttribute), attrType);

            var attr = tooling.GetAttributes(attrType, functionMetadata);
            Assert.Equal(1, attr.Length);
            Assert.Equal("mypath", ((EventHubAttribute)attr[0]).EventHubName);

            // $$$ Verify in config?

            var defaultType = tooling.GetDefaultType(FileAccess.Write, Cardinality.One, DataType.Binary, attr[0]);
            Assert.Equal(typeof(IAsyncCollector<byte[]>), defaultType);
        }

        // Test 'Consumer group' property gets to attribute.
        [Fact]
        public async Task EventHubTriggerWithConsumerGroup()
        {
            var config = GetConfig();

            // JObject from Function.Json
            var functionMetadata = JObject.Parse(@"
    {
        'type': 'eventHubTrigger',
        'connection': 'MyConnection',
        'path' : '%foo%',
        'consumerGroup': 'MyConsumerGroup'
    }");

            ToolingHelper tooling = new ToolingHelper(config);

            // Pull in the extension
            JObject hostMetadata = null;
            await tooling.AddAssemblyAsync(typeof(EventHubConfiguration).Assembly, hostMetadata);
            await tooling.FinishAddsAsync();

            string name = functionMetadata["type"].ToString();
            var attrType = tooling.GetAttributeTypeFromName(name);
            Assert.Equal(typeof(EventHubTriggerAttribute), attrType);

            var attributes = tooling.GetAttributes(attrType, functionMetadata);
            Assert.Equal(1, attributes.Length);

            var attr = (EventHubTriggerAttribute) attributes[0];

            Assert.Equal("mypath", attr.EventHubName);
            Assert.Equal("MyConsumerGroup", attr.ConsumerGroup);
        }

        [Fact]
        public async Task EventHubTrigger()
        {
            var config = GetConfig();

            // JObject from Function.Json
            var functionMetadata = JObject.Parse(@"
    {
        'type': 'eventHubTrigger',
        'name': 'x',
        'direction': 'in',
        'connection': 'MyConnection',
        'path' : '%foo%'
    }");

            ToolingHelper tooling = new ToolingHelper(config);

            // Pull in the extension
            JObject hostMetadata = null;
            await tooling.AddAssemblyAsync(typeof(EventHubConfiguration).Assembly, hostMetadata);
            await tooling.FinishAddsAsync();

            string name = functionMetadata["type"].ToString();
            var attrType = tooling.GetAttributeTypeFromName(name);
            Assert.Equal(typeof(EventHubTriggerAttribute), attrType);

            var attr = tooling.GetAttributes(attrType, functionMetadata);
            Assert.Equal(1, attr.Length);
            Assert.Equal("mypath", ((EventHubTriggerAttribute)attr[0]).EventHubName);

            {
                var defaultType = tooling.GetDefaultType(FileAccess.Read, Cardinality.One, DataType.Binary, attr[0]);
                Assert.Equal(typeof(byte[]), defaultType);
            }
            {
                var defaultType = tooling.GetDefaultType(FileAccess.Read, Cardinality.One, DataType.String, attr[0]);
                Assert.Equal(typeof(string), defaultType);
            }
            {
                var defaultType = tooling.GetDefaultType(FileAccess.Read, Cardinality.Many, DataType.Binary, attr[0]);
                Assert.Equal(typeof(byte[][]), defaultType);
            }
            {
                var defaultType = tooling.GetDefaultType(FileAccess.Read, Cardinality.Many, DataType.String, attr[0]);
                Assert.Equal(typeof(string[]), defaultType);
            }       
        }
    }
}
