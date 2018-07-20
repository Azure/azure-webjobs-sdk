// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class JobHostMetadataProviderTests
    {
        [Fact]
        public void Test()
        {          
            var ext = new TestExtension();

            var host = new HostBuilder()
                .ConfigureDefaultTestHost<MyProg>()
                .AddExtension(ext)
                .Build();
            IJobHostMetadataProvider metadataProvider = host.CreateMetadataProvider();
            Assert.Equal(1, ext._counter);

            // Callable            
            host.GetJobHost<MyProg>().Call("Test");
            Assert.Equal(1, ext._counter);

            // Fact that we registered a Widget converter is enough to add the assembly 
            Assembly asm;
            bool resolved;

            resolved = metadataProvider.TryResolveAssembly(typeof(Widget).Assembly.GetName().Name, out asm);
            Assert.True(resolved);
            Assert.Same(asm, typeof(Widget).Assembly);

            // check with full name 
            resolved = metadataProvider.TryResolveAssembly(typeof(Widget).Assembly.GetName().FullName, out asm);
            Assert.True(resolved);
            Assert.Same(asm, typeof(Widget).Assembly);

            // This requires the target attribute to be unique within the assembly. 
            var attrType = metadataProvider.GetAttributeTypeFromName("Test9");
            Assert.Equal(typeof(Test9Attribute), attrType);

            // JObject --> Attribute 
            var attr = GetAttr<Test9Attribute>(metadataProvider, new { Flag = "xyz" });
            Assert.Equal("xyz", attr.Flag);

            // Getting default type. 
            var defaultType = metadataProvider.GetDefaultType(attr, FileAccess.Read, null);
            Assert.Equal(typeof(JObject), defaultType);

            // If we have no match for output, we'll try IAsyncCollector<string>
            Assert.Equal(typeof(IAsyncCollector<string>), metadataProvider.GetDefaultType(attr, FileAccess.Write, typeof(object)));
        }

        static T GetAttr<T>(IJobHostMetadataProvider metadataProvider, object obj) where T : Attribute
        {
            var attribute = metadataProvider.GetAttribute(typeof(T), JObject.FromObject(obj));
            return (T)attribute;
        }

        // This is a setup used by CosmoDb. 
        [Fact]
        public void DefaultTypeForOpenTypeCollector()
        {
            var ext = new TestExtension2();
            var host = new HostBuilder()
                .ConfigureDefaultTestHost()
                .AddExtension(ext)
                .Build();

            IJobHostMetadataProvider metadataProvider = host.CreateMetadataProvider();

            var attr = new Test9Attribute(null);
            var type = metadataProvider.GetDefaultType(attr, FileAccess.Write, null);

            // The collector handles Open type, which means it will first pull byte[]. 
            Assert.Equal(typeof(IAsyncCollector<byte[]>), type);
        }

        // Setup similar to CosmoDb
        public class TestExtension2 : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
                var ignored = typeof(object); // not used 
                context.AddBindingRule<Test9Attribute>().BindToCollector<OpenType>(ignored);
            }
        }

        // Verify for a Jobject-only collector. 
        [Fact]
        public void DefaultTypeForJObjectCollector()
        {
            var ext = new TestExtension3();

            var host = new HostBuilder()
                .ConfigureDefaultTestHost()
                .AddExtension(ext)
                .Build();

            IJobHostMetadataProvider metadataProvider = host.CreateMetadataProvider();

            var attr = new Test9Attribute(null);
            var type = metadataProvider.GetDefaultType(attr, FileAccess.Write, null);

            // Explicitly should be Jobject since that's all the collector is registered as.
            Assert.Equal(typeof(IAsyncCollector<JObject>), type);
        }

        public class TestExtension3 : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
                context.AddBindingRule<Test9Attribute>().
                    BindToCollector<JObject>(attr => (IAsyncCollector<JObject>)null);
            }
        }


        [Fact]
        public void DefaultTypeForTrigger()
        {
            var ext = new JArrayTriggerExtension();
            var host = new HostBuilder()
                 .ConfigureDefaultTestHost()
                 .ConfigureTypeLocator() // empty 
                 .AddExtension(ext)
                 .Build();

            IJobHostMetadataProvider metadataProvider = host.CreateMetadataProvider();

            var attr = new Test9Attribute(null);
            var type = metadataProvider.GetDefaultType(attr, FileAccess.Read, null);

            Assert.Equal(typeof(JArray), type);
        }

        public class JArrayTriggerExtension : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
                var rule = context.AddBindingRule<Test9Attribute>();
                rule.BindToTrigger<string>();
                rule.AddConverter<string, JArray>(input => (JArray)null);
            }
        }

        [Fact]
        public void DefaultTypeForOpenTypeTrigger()
        {
            var ext = new OpenTypeTriggerExtension();
            var host = new HostBuilder()
                 .ConfigureDefaultTestHost()
                 .ConfigureTypeLocator() // empty 
                 .AddExtension(ext)
                 .Build();
            IJobHostMetadataProvider metadataProvider = host.CreateMetadataProvider();

            var attr = new Test9Attribute(null);
            var type = metadataProvider.GetDefaultType(attr, FileAccess.Write, null);

            // The trigger handles Open type, which means it will first pull byte[]. 
            Assert.Equal(typeof(byte[]), type);
        }

        public class OpenTypeTriggerExtension : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
                var rule = context.AddBindingRule<Test9Attribute>();
                rule.BindToTrigger<string>();
                rule.AddOpenConverter<string, OpenType>((a, b, c) => null);
            }
        }

        [Fact]
        public void GetFunctionMetadata()
        {
            var mockFunctionIndexProvider = new Mock<IFunctionIndexProvider>();

            var functionDescriptor = new FunctionDescriptor()
            {
                IsDisabled = true
            };
            var mockFunctionIndex = new Mock<IFunctionIndex>();
            mockFunctionIndex.Setup(i => i.LookupByName("testMethod")).Returns(new FunctionDefinition(functionDescriptor, null, null));
            var token = new CancellationToken();
            mockFunctionIndexProvider.Setup(p => p.GetAsync(token)).Returns(Task.FromResult(mockFunctionIndex.Object));

            Func<IFunctionIndexProvider> getter = (() =>
            {
                return mockFunctionIndexProvider.Object;
            });

            IJobHostMetadataProvider provider = new JobHostMetadataProvider(mockFunctionIndexProvider.Object, null, null, null);

            var functionMetadata = provider.GetFunctionMetadata("testNotExists");
            Assert.Equal(functionMetadata, null);

            functionMetadata = provider.GetFunctionMetadata("testMethod");
            Assert.Equal(functionMetadata.IsDisabled, true);
        }

        // Give this a unique name within the assembly so that the name --> type 
        // reverse lookup can be unambiguous. 
        [Binding]
        public class Test9Attribute : Attribute
        {
            public Test9Attribute(string flag)
            {
                this.Flag = flag;
            }
            public string Flag { get; set; }
        }

        public class Widget
        {
            public string Value;
        }

        public class TestExtension : IExtensionConfigProvider
        {
            public int _counter;

            public void Initialize(ExtensionConfigContext context)
            {
                _counter++;
                context.AddBindingRule<Test9Attribute>().
                    BindToInput<Widget>(Builder);

                context.AddConverter<Widget, JObject>(widget => JObject.FromObject(widget));
            }

            Widget Builder(Test9Attribute input)
            {
                return new Widget { Value = input.Flag };
            }
        }

        public class MyProg
        {
            public string _value;
            public void Test([Test9("f1")] Widget w)
            {
                _value = w.Value;
            }
        }
    }
}
