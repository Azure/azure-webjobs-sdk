// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{

    // $$$ Add error cases. 

    public class BindingX // $$$
    {
        class Empty { } 
        // Checks that we write the marker file when we call the host
        [Fact]
        public async Task Queue()
        {            
            var config = TestHelpers.NewConfig(typeof(Empty));

            var queueName = "test-input-byte";

            // JObject from Function.Json
            var functionMetadata = JObject.Parse(@"
    {
        'type': 'queue',
        'name': 'output',
        'direction': 'out',
        'queueName': '"+ queueName+@"'
    }");
           
            ToolingHelper tooling = new ToolingHelper(config);
            await tooling.FinishAddsAsync();
            string name = functionMetadata["type"].ToString();
            var attrType = tooling.GetAttributeTypeFromName(name);
            Assert.Equal(typeof(QueueAttribute), attrType);

            var attr = tooling.GetAttributes(attrType, functionMetadata);
            Assert.Equal(1, attr.Length);
            Assert.Equal(queueName, ((QueueAttribute)attr[0]).QueueName);

            var defaultType = tooling.GetDefaultType(FileAccess.Write, Cardinality.One, DataType.Binary, attr[0]);
            Assert.Equal(typeof(IAsyncCollector<byte[]>), defaultType);

            var defaultType2 = tooling.GetDefaultType(FileAccess.Read, Cardinality.One, DataType.String, attr[0]);
            Assert.Equal(typeof(string), defaultType2);

            var defaultType3 = tooling.GetDefaultType(FileAccess.Read, Cardinality.One, DataType.Binary, attr[0]);
            Assert.Equal(typeof(byte[]), defaultType3);
        }

        // Test specifying the 'connection' property 
        [Fact]
        public async Task QueueWithDifferentConnection()
        {
            var config = TestHelpers.NewConfig(typeof(Empty));

            var queueName = "test-input-byte";

            // JObject from Function.Json
            var functionMetadata = JObject.Parse(@"
    {
        'type': 'queue',
        'name': 'output',
        'direction': 'out',
'Connection' : 'mycx',
        'queueName': '" + queueName + @"'

    }");

            ToolingHelper tooling = new ToolingHelper(config);
            await tooling.FinishAddsAsync();
            string name = functionMetadata["type"].ToString();
            var attrType = tooling.GetAttributeTypeFromName(name);
            Assert.Equal(typeof(QueueAttribute), attrType);

            var attributes = tooling.GetAttributes(attrType, functionMetadata);
            Assert.Equal(2, attributes.Length);

            Assert.Equal(queueName, ((QueueAttribute)attributes[0]).QueueName);
            Assert.Equal("mycx", ((StorageAccountAttribute)attributes[1]).Account);
        }

        [Fact]
        public async Task BlobTrigger()
        {
            var config = TestHelpers.NewConfig(typeof(Empty));

            // JObject from Function.Json
            var functionMetadata = JObject.Parse(@"
          {
            'type': 'blobTrigger',
            'name': 'input',
            'direction': 'in',
            'dataType': 'binary',
            'path': 'test-input-node/{name}'
        }");


            ToolingHelper tooling = new ToolingHelper(config);
            await tooling.FinishAddsAsync();

            string name = functionMetadata["type"].ToString();
            var attrType = tooling.GetAttributeTypeFromName(name);
            Assert.Equal(typeof(BlobTriggerAttribute), attrType);

            var attributes = tooling.GetAttributes(attrType, functionMetadata);
            Assert.Equal(1, attributes.Length);
            var attr = (BlobTriggerAttribute) attributes[0];

            Assert.Equal("test-input-node/{name}", attr.BlobPath);


            // JObject since attribute has Parition and RowKey 
            var defaultType = tooling.GetDefaultType(FileAccess.Write, Cardinality.One, DataType.Stream, attr);
            Assert.Equal(typeof(Stream), defaultType);
        }

        [Fact]
        public async Task BlobIn()
        {
            var config = TestHelpers.NewConfig(typeof(Empty));

            // JObject from Function.Json
            var functionMetadata = JObject.Parse(@"
          {
            'type': 'blob',
            'name': 'x',
            'direction': 'in',
            'path': 'test-input-node/{name}'
        }");

            ToolingHelper tooling = new ToolingHelper(config);
            await tooling.FinishAddsAsync();

            string name = functionMetadata["type"].ToString();
            var attrType = tooling.GetAttributeTypeFromName(name);
            Assert.Equal(typeof(BlobAttribute), attrType);

            var attributes = tooling.GetAttributes(attrType, functionMetadata);
            Assert.Equal(1, attributes.Length);
            var attr = (BlobAttribute)attributes[0];

            Assert.Equal("test-input-node/{name}", attr.BlobPath);
            Assert.Equal(FileAccess.Read, attr.Access);

            // JObject since attribute has Parition and RowKey 
            var defaultType = tooling.GetDefaultType(FileAccess.Read, Cardinality.One, DataType.Stream, attr);
            Assert.Equal(typeof(Stream), defaultType);
        }

        [Fact]
        public async Task BlobOut()
        {
            var config = TestHelpers.NewConfig(typeof(Empty));

            // JObject from Function.Json
            var functionMetadata = JObject.Parse(@"
          {
            'type': 'blob',
            'name': 'input',
            'direction': 'out',
            'dataType': 'binary',
            'path': 'test-input-node/{name}'
        }");

            ToolingHelper tooling = new ToolingHelper(config);
            await tooling.FinishAddsAsync();

            string name = functionMetadata["type"].ToString();
            var attrType = tooling.GetAttributeTypeFromName(name);
            Assert.Equal(typeof(BlobAttribute), attrType);

            var attributes = tooling.GetAttributes(attrType, functionMetadata);
            Assert.Equal(1, attributes.Length);
            var attr = (BlobAttribute)attributes[0];

            Assert.Equal("test-input-node/{name}", attr.BlobPath);
            Assert.Equal(FileAccess.Write, attr.Access);
            
            // JObject since attribute has Parition and RowKey 
            var defaultType = tooling.GetDefaultType(FileAccess.Read, Cardinality.One, DataType.Stream, attr);
            Assert.Equal(typeof(Stream), defaultType);
        }
        
        [Fact]
        public async Task TableReadAsAsyncCollector()
        {
            var config = TestHelpers.NewConfig(typeof(Empty));

            // JObject from Function.Json
            var functionMetadata = JObject.Parse(@"
   {
            'type': 'table',
            'name': 'single',
            'direction': 'out',
            'tableName': 'test'            
        }");


            ToolingHelper tooling = new ToolingHelper(config);
            await tooling.FinishAddsAsync();
            string name = functionMetadata["type"].ToString();

            var attrType = tooling.GetAttributeTypeFromName(name);
            Assert.Equal(typeof(TableAttribute), attrType);

            var attr = tooling.GetAttributes(attrType, functionMetadata);
            Assert.Equal(1, attr.Length);

            // JObject since attribute has Parition and RowKey 
            var defaultType = tooling.GetDefaultType(FileAccess.Write, Cardinality.One, DataType.String, attr[0]);
            Assert.Equal(typeof(IAsyncCollector<JObject>), defaultType);
        }

        // Test other properties on the table attribute. 
        [Fact]
        public async Task TableParameters()
        {
            var config = TestHelpers.NewConfig(typeof(Empty));

            // JObject from Function.Json
            var functionMetadata = JObject.Parse(@"
   {
            'type': 'table',
            'name': 'single',
            'direction': 'in',
            'tableName': 'test',
            'partitionKey': 'AAA',
            'take' : 15,
            'filter' : 'myfilter'
        }");


            ToolingHelper tooling = new ToolingHelper(config);
            await tooling.FinishAddsAsync();
            string name = functionMetadata["type"].ToString();

            var attrType = tooling.GetAttributeTypeFromName(name);
            Assert.Equal(typeof(TableAttribute), attrType);

            var attr = tooling.GetAttributes(attrType, functionMetadata);
            Assert.Equal(1, attr.Length);
            var tableAttr = ((TableAttribute)attr[0]);
            Assert.Equal("test", tableAttr.TableName);
            Assert.Equal("AAA", tableAttr.PartitionKey);
            Assert.Equal(null, tableAttr.RowKey);

            Assert.Equal(15, tableAttr.Take);
            Assert.Equal("myfilter", tableAttr.Filter);
        }

        [Fact]
        public async Task TableReadAsJObject()
        {
            var config = TestHelpers.NewConfig(typeof(Empty));
            
            // JObject from Function.Json
            var functionMetadata = JObject.Parse(@"
   {
            'type': 'table',
            'name': 'single',
            'direction': 'in',
            'tableName': 'test',
            'partitionKey': 'AAA',
            'rowKey': '001'
        }");            

            ToolingHelper tooling = new ToolingHelper(config);
            await tooling.FinishAddsAsync();
            string name = functionMetadata["type"].ToString();

            var attrType = tooling.GetAttributeTypeFromName(name);
            Assert.Equal(typeof(TableAttribute), attrType);

            var attr = tooling.GetAttributes(attrType, functionMetadata);
            Assert.Equal(1, attr.Length);
            var tableAttr = ((TableAttribute)attr[0]);
            Assert.Equal("test", tableAttr.TableName);
            Assert.Equal("AAA", tableAttr.PartitionKey);
            Assert.Equal("001", tableAttr.RowKey);

            // JObject since attribute has Parition and RowKey 
            var defaultType = tooling.GetDefaultType(FileAccess.Read, Cardinality.One, DataType.String, attr[0]);
            Assert.Equal(typeof(JObject), defaultType);
        }

        [Fact]
        public async Task TableReadAsJArray()
        {
            var config = TestHelpers.NewConfig(typeof(Empty));

            // JObject from Function.Json
            var functionMetadata = JObject.Parse(@"
   {
            'type': 'table',
            'name': 'single',
            'direction': 'in',
            'tableName': 'test',
            'partitionKey': 'AAA'            
        }");

            ToolingHelper tooling = new ToolingHelper(config);
            await tooling.FinishAddsAsync();
            string name = functionMetadata["type"].ToString();

            var attrType = tooling.GetAttributeTypeFromName(name);
            Assert.Equal(typeof(TableAttribute), attrType);

            var attr = tooling.GetAttributes(attrType, functionMetadata);
            Assert.Equal(1, attr.Length);
            var tableAttr = ((TableAttribute)attr[0]);
            Assert.Equal("test", tableAttr.TableName);
            Assert.Equal("AAA", tableAttr.PartitionKey);
            Assert.Equal(null, tableAttr.RowKey);

            // JArray since we're missing row key.
            var defaultType = tooling.GetDefaultType(FileAccess.Read, Cardinality.One, DataType.String, attr[0]);
            Assert.Equal(typeof(JArray), defaultType);
        }
    }
}