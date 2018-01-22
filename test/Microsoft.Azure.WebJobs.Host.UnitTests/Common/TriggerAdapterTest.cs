using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Triggers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Common
{
    // Test for Trigger adapter class. 
    public class TriggerAdapterTest
    {
        class MyTriggerBindingProvider : ITriggerBindingProvider
        {
            public async Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
            {
                return new MyTriggerBinding();
            }
        
        }
        class MyTriggerBinding : ITriggerBinding
        {
            public Type TriggerValueType => typeof(FakeQueueData);

            public IReadOnlyDictionary<string, Type> BindingDataContract => new Dictionary<string, Type>(); // empty

            public async Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
            {
                return new TriggerData(new Dictionary<string, object>());
            }

            public async Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
            {
                return new NullListener();
            }

            public ParameterDescriptor ToParameterDescriptor()
            {
                return new ParameterDescriptor();
            }
        }

        // Extension testing BindToTrigger
        class Ext : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
                var rule = context.AddBindingRule<FakeQueueTriggerAttribute>();
                rule.BindToTrigger<FakeQueueData>(new MyTriggerBindingProvider());

                rule.AddConverter<FakeQueueData, string>(
                    msg => msg.Message);

                rule.AddConverter<FakeQueueData, int>(
                    msg => int.Parse(msg.Message));
            }
        }

        // Extension testing BindToTrigger
        // Extension that does not have a string converter. 
        class ExtNoStringConverter : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
                var rule = context.AddBindingRule<FakeQueueTriggerAttribute>();
                rule.BindToTrigger<FakeQueueData>(new MyTriggerBindingProvider());

                rule.AddConverter<FakeQueueData, int>(
                    msg => int.Parse(msg.Message));
            }
        }

        // Program to test with the NoString converter scenario 
        public class ProgNoString
        {
            public static object _value = null;

            public static void FuncAsInt([FakeQueueTrigger] int data)
            {
                _value = data;
            }
        }

        public class Prog
        {
            public static object _value = null;

            public static void FuncAsString([FakeQueueTrigger] string data)
            {
                _value = data;
            }

            public static void FuncAsInt([FakeQueueTrigger] int data)
            {
                _value = data;
            }
        }

        [Fact]
        public async Task TestNoStringTriggerAdapter()
        {
            var queueClient = new FakeQueueClient();
            var config = TestHelpers.NewConfig<ProgNoString>(new ExtNoStringConverter(), queueClient);
            var host = new JobHost(config);

            var args = new Dictionary<string, object>();
            args["data"] = new FakeQueueData { Message = "15" };

            await host.CallAsync("FuncAsInt", args);
            Assert.Equal(15, ProgNoString._value);
        }

        [Fact]
        public async Task TestTriggerAdapter()
        {
            var queueClient = new FakeQueueClient();
            var config = TestHelpers.NewConfig<Prog>(new Ext(), queueClient);
            var host = new JobHost(config);

            var args = new Dictionary<string, object>();
            args["data"] = new FakeQueueData { Message = "15" };

            // Test various converters. 
            await host.CallAsync("FuncAsString", args);
            Assert.Equal("15", Prog._value);

            await host.CallAsync("FuncAsInt", args);
            Assert.Equal(15, Prog._value);
        }
    }
}
