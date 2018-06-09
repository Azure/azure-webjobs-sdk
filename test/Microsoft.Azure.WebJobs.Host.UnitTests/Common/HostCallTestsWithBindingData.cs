﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    // Unit test for exercising Host.Call passing route data. 
    public class HostCallTestsWithBindingData
    {
        public class FunctionBase
        {
            // Derived functions write to this variable, test harness can read from it.
            public StringBuilder _sb = new StringBuilder();
        }

        public class Functions : FunctionBase
        {
            public void Func(
                [Test(Path = "{k1}-x")] string p1,
                [Test(Path = "{k2}-y")] string p2,
                int k1)
            {
                _sb.AppendFormat("{0};{1};{2}", p1, p2, k1);
            }
        }

        public class Functions2 : FunctionBase
        {
            public class Payload
            {
                public int k1 { get; set; }
                public int k2 { get; set; }
            }

            public void Func(
                [FakeQueueTrigger] Payload trigger,
                [Test(Path = "{k1}-x")] string p1,
                [Test(Path = "{k2}-y")] string p2,
                int k1)
            {
                _sb.AppendFormat("{0};{1};{2}", p1, p2, k1);
            }
        }

        [Binding]
        public class TestAttribute : Attribute
        {
            [AutoResolve]
            public string Path { get; set; }
        }

        public class FakeExtClient : IExtensionConfigProvider, IConverter<TestAttribute, string>
        {
            private readonly INameResolver _nameResolver;
            private readonly IConverterManager _converterManager;

            public FakeExtClient(INameResolver nameResolver, IConverterManager converterManager)
            {
                _nameResolver = nameResolver;
                _converterManager = converterManager;
            }

            public void Initialize(ExtensionConfigContext context)
            {
                // Add [Test] support
                var rule = context.AddBindingRule<TestAttribute>();
                rule.BindToInput<string>(typeof(FakeExtClient), _nameResolver, _converterManager);

                // Add [FakeQueueTrigger] support.                 
                context.AddConverter<string, FakeQueueData>(x => new FakeQueueData { Message = x });
                context.AddConverter<FakeQueueData, string>(msg => msg.Message);

                var triggerBindingProvider = new FakeQueueTriggerBindingProvider(new FakeQueueClient(_nameResolver, _converterManager), _converterManager);
                context.AddBindingRule<FakeQueueTriggerAttribute>().BindToTrigger(triggerBindingProvider);
            }

            public string Convert(TestAttribute attr)
            {
                return attr.Path;
            }
        }

        // Explicit bindingData takes precedence over binding data inferred from the trigger object. 
        [Fact]
        public async Task InvokeTrigger()
        {
            var obj = new Functions2.Payload
            {
                k1 = 100,
                k2 = 200
            };

            string result = await Invoke<Functions2>(new
            {
                trigger = NewTriggerObject(obj), // supplies k1,k2
                k1 = 111 // overwrites trigger.k1
            });
            Assert.Equal("111-x;200-y;111", result);
        }

        // [FakeQueueTrigger] expects object to come as a FakeQueueDataBatch
        private static FakeQueueDataBatch NewTriggerObject(Functions2.Payload obj)
        {
            return new FakeQueueDataBatch
            {
                Events = new FakeQueueData[]
                {
                    new FakeQueueData
                    {
                        Message = JsonConvert.SerializeObject(obj)
                    }
                }
            };
        }

        // Invoke with binding data only, no parameters. 
        [Fact]
        public async Task InvokeWithBindingData()
        {
            string result = await Invoke<Functions>(new { k1 = 100, k2 = 200 });
            Assert.Equal("100-x;200-y;100", result);
        }

        // Providing a direct parameter takes precedence overbinding data
        [Fact]
        public async Task Parameter_Takes_Precedence()
        {
            string result = await Invoke<Functions>(new { k1 = 100, k2 = 200, p1 = "override" });
            Assert.Equal("override;200-y;100", result);
        }

        // Get an error when missing values. 
        [Fact]
        public async Task Missing()
        {
            try
            {
                string result = await Invoke<Functions>(new { k1 = 100 });
            }
            catch (FunctionInvocationException e)
            {
                // There error should specifically be with p2. p1 and k1 binds ok since we supplied k1. 
                var msg1 = "Exception binding parameter 'p2'";
                Assert.Equal(msg1, e.InnerException.Message);

                var msg2 = "No value for named parameter 'k2'.";
                Assert.Equal(msg2, e.InnerException.InnerException.Message);
                return;
            }
            Assert.True(false, "Invoker should have failed");
        }

        // Helper to invoke the method with the given parameters
        private async Task<string> Invoke<TFunction>(object arguments) where TFunction : FunctionBase, new()
        {
            var activator = new FakeActivator();
            TFunction testInstance = new TFunction();
            activator.Add(testInstance);

            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<TFunction>()
                .ConfigureServices(services => services.AddSingleton<IJobActivator>(activator))
                .AddExtension<FakeExtClient>()
                .Build();

            await host.GetJobHost().CallAsync(typeof(TFunction).GetMethod("Func"), arguments);

            var x = testInstance._sb.ToString();

            return x;
        }
    }
}
