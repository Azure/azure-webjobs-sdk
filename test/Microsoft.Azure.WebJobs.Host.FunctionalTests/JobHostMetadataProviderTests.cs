// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    // Test MetadataProvider with Storage attributes. 
    public class JobHostMetadataProviderTests
    {
        static T GetAttr<T>(IJobHostMetadataProvider metadataProvider, object obj) where T : Attribute
        {
            var attribute = metadataProvider.GetAttribute(typeof(T), JObject.FromObject(obj));
            return (T)attribute;
        }

        class MyTriggerBinding : ITriggerBinding
        {
            public Type TriggerValueType => typeof(SomeData);

            public IReadOnlyDictionary<string, Type> BindingDataContract => new Dictionary<string, Type>(); // empty

            public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
            {
                return Task.FromResult<ITriggerData>(new TriggerData(new Dictionary<string, object>()));
            }

            public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
            {
                return Task.FromResult<IListener>(new NullListener());
            }

            public ParameterDescriptor ToParameterDescriptor()
            {
                return new ParameterDescriptor();
            }
        }

        class MyTriggerBindingProvider : ITriggerBindingProvider
        {
            public Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
            {
                return Task.FromResult<ITriggerBinding>(new MyTriggerBinding());
            }
        }

        [Binding]
        public class TestAttribute : Attribute
        {
        }

        public class SomeData
        {
            public string Message { get; set; }
        }

        class FluentExtensionConfigStringBatch : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
                var rule = context.AddBindingRule<TestAttribute>();
                rule.BindToTrigger<SomeData[]>(new MyTriggerBindingProvider());

                rule.AddConverter<SomeData[], string[]>(
                    msgs => msgs.Select(m => m.Message).ToArray());
            }
        }

        class FluentExtensionConfigJObjectBatch : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
                var rule = context.AddBindingRule<TestAttribute>();
                rule.BindToTrigger<SomeData[]>(new MyTriggerBindingProvider());

                rule.AddConverter<SomeData[], JObject[]>(
                    msgs => msgs.Select(m => JObject.Parse(m.Message)).ToArray());
            }
        }

        class FluentExtensionConfigBytesBatch : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
                var rule = context.AddBindingRule<TestAttribute>();
                rule.BindToTrigger<SomeData[]>(new MyTriggerBindingProvider());

                rule.AddConverter<SomeData[], byte[][]>(
                    msgs => msgs.Select(m => Encoding.ASCII.GetBytes(m.Message)).ToArray());

                rule.AddConverter<SomeData[], string[]>(
                    msgs => msgs.Select(m => m.Message).ToArray());
            }
        }

        class FluentExtensionConfigBytesSingle : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
                var rule = context.AddBindingRule<TestAttribute>();
                rule.BindToTrigger<SomeData>(new MyTriggerBindingProvider());

                rule.AddConverter<SomeData, byte[]>(msg => Encoding.ASCII.GetBytes(msg.Message));

                rule.AddConverter<SomeData[], byte[][]>(
                    msgs => msgs.Select(m => Encoding.ASCII.GetBytes(m.Message)).ToArray());
            }
        }

        class FluentExtensionConfigDefaultBatch : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
                var rule = context.AddBindingRule<TestAttribute>();
                rule.BindToTrigger<SomeData[]>(new MyTriggerBindingProvider());

                rule.AddConverter<SomeData[], int[]>(
                    msgs => msgs.Select(m => m.Message.Length).ToArray());
            }
        }

        [Fact]
        public void AttrBuilder()
        {
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost(b =>
                {
                    b.AddAzureStorageBlobs();
                    b.AddAzureStorageQueues();
                })
                .Build();
            
            var metadataProvider = host.CreateMetadataProvider();

            // Blob 
            var blobAttr = GetAttr<BlobAttribute>(metadataProvider, new { path = "x" });
            Assert.Equal("x", blobAttr.BlobPath);
            Assert.Equal(null, blobAttr.Access);

            blobAttr = GetAttr<BlobAttribute>(metadataProvider, new { path = "x", direction = "in" });
            Assert.Equal("x", blobAttr.BlobPath);
            Assert.Equal(FileAccess.Read, blobAttr.Access);

            blobAttr = GetAttr<BlobAttribute>(metadataProvider, new { Path = "x", Direction = "out" });
            Assert.Equal("x", blobAttr.BlobPath);
            Assert.Equal(FileAccess.Write, blobAttr.Access);

            blobAttr = GetAttr<BlobAttribute>(metadataProvider, new { path = "x", direction = "inout" });
            Assert.Equal("x", blobAttr.BlobPath);
            Assert.Equal(FileAccess.ReadWrite, blobAttr.Access);

            blobAttr = GetAttr<BlobAttribute>(metadataProvider,
            new
            {
                path = "x",
                direction = "in",
                connection = "cx1"
            });
            Assert.Equal("x", blobAttr.BlobPath);
            Assert.Equal(FileAccess.Read, blobAttr.Access);
            Assert.Equal("cx1", blobAttr.Connection);

            blobAttr = GetAttr<BlobAttribute>(metadataProvider,
              new
              {
                  path = "x",
                  direction = "in",
                  connection = "" // empty, not null 
              });
            Assert.Equal("x", blobAttr.BlobPath);
            Assert.Equal(FileAccess.Read, blobAttr.Access);
            Assert.Equal("", blobAttr.Connection); // empty is passed straight through. 

            var blobTriggerAttr = GetAttr<BlobTriggerAttribute>(metadataProvider, new { path = "x" });
            Assert.Equal("x", blobTriggerAttr.BlobPath);

            // Queue 
            var queueAttr = GetAttr<QueueAttribute>(metadataProvider, new { QueueName = "q1" });
            Assert.Equal("q1", queueAttr.QueueName);

            var queueTriggerAttr = GetAttr<QueueTriggerAttribute>(metadataProvider, new { QueueName = "q1" });
            Assert.Equal("q1", queueTriggerAttr.QueueName);
        }

        [Fact]
        public void DefaultTypeForQueue()
        {
            var host = new HostBuilder()
                .ConfigureDefaultTestHost(b =>
                {
                    b.AddAzureStorageQueues();
                })
                .Build();

            var metadataProvider = host.CreateMetadataProvider();

            var t1 = metadataProvider.GetDefaultType(new QueueTriggerAttribute("q"), FileAccess.Read, typeof(byte[]));
            Assert.Equal(typeof(byte[]), t1);

            var t2 = metadataProvider.GetDefaultType(new QueueTriggerAttribute("q"), FileAccess.Read, null);
            Assert.Equal(typeof(string), t2);

            // Very important that this is byte[]. 
            // Script doesn't require Function.json for JScript to specify datatype. 
            // JScript can convert Jobject, string to byte[].
            // But can't convert byte[] to JObject. 
            // so byte[] is the safest default. 
            var t3 = metadataProvider.GetDefaultType(new QueueAttribute("q"), FileAccess.Write, null);
            Assert.Equal(typeof(IAsyncCollector<byte[]>), t3);
        }

        [Theory]
        [InlineData(typeof(FluentExtensionConfigStringBatch), typeof(string[]), typeof(string[]))]
        [InlineData(typeof(FluentExtensionConfigStringBatch), typeof(byte[][]), typeof(string[]))]
        [InlineData(typeof(FluentExtensionConfigStringBatch), typeof(JObject[]), typeof(string[]))]
        [InlineData(typeof(FluentExtensionConfigStringBatch), typeof(object[]), typeof(string[]))]
        [InlineData(typeof(FluentExtensionConfigJObjectBatch), typeof(string[][]), typeof(JObject[]))]
        [InlineData(typeof(FluentExtensionConfigJObjectBatch), typeof(byte[][]), typeof(JObject[]))]
        [InlineData(typeof(FluentExtensionConfigJObjectBatch), typeof(JObject[]), typeof(JObject[]))]
        [InlineData(typeof(FluentExtensionConfigJObjectBatch), typeof(object[]), typeof(JObject[]))]
        [InlineData(typeof(FluentExtensionConfigBytesBatch), typeof(string[][]), typeof(byte[][]))]
        [InlineData(typeof(FluentExtensionConfigBytesBatch), typeof(byte[][]), typeof(byte[][]))]
        [InlineData(typeof(FluentExtensionConfigBytesBatch), typeof(JObject[]), typeof(byte[][]))]
        [InlineData(typeof(FluentExtensionConfigBytesBatch), typeof(object[]), typeof(byte[][]))]
        [InlineData(typeof(FluentExtensionConfigDefaultBatch), typeof(string[][]), typeof(object[]))]
        [InlineData(typeof(FluentExtensionConfigDefaultBatch), typeof(byte[][]), typeof(object[]))]
        [InlineData(typeof(FluentExtensionConfigDefaultBatch), typeof(JObject[]), typeof(object[]))]
        [InlineData(typeof(FluentExtensionConfigDefaultBatch), typeof(object[]), typeof(object[]))]
        [InlineData(typeof(FluentExtensionConfigBytesSingle), typeof(byte[]), typeof(byte[]))]
        public void TestBatchFluentDefaultType(Type extensionConfigProvider, Type requestedType, Type expectedType)
        {
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost(b =>
                {
                    b.AddExtension(extensionConfigProvider);
                })
                .Build();

            var metadataProvider = host.CreateMetadataProvider();

            var dataType = metadataProvider.GetDefaultType(new TestAttribute(), FileAccess.Read, requestedType);
            Assert.Equal(expectedType, dataType);
        }
    }
}
