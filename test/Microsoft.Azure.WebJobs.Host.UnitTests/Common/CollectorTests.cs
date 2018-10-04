// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    // Test IAsyncCollector binding permutations using the FakeQueue mocks. 
    public class CollectorTests
    {
        // dummy Poco to use with test. 
        public class Payload
        {
            public int val1 { get; set; }
        }

        public class DerivedFakeQueueData : FakeQueueData
        {
            [JsonIgnore]
            public string Bonus { get; set; }
        }

        public class ErrorProgram
        {
            // Malformed 
            // Expect the 
            public static void Func([FakeQueue(Prefix = "Error-{name%")] out string x)
            {
                x = "x";
            }

            [NoAutomaticTrigger]
            public static void ValidMethod()
            {
            }
        }

        [Fact]
        public void TestError()
        {
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<ErrorProgram>(b=>
                {
                    b.AddExtension<FakeQueueClient>();
                })
                .Build();

            FakeQueueClient client = host.GetExtension<FakeQueueClient>();
            JobHost jobHost = host.GetJobHost();
            var p = host.GetTestLoggerProvider();

            // Call 'ok' method which has no errors. Should still get the indexing errors from the other method. 
            var m = typeof(ErrorProgram).GetMethod(nameof(ErrorProgram.ValidMethod));

            try
            {
                jobHost.CallAsync(m); // Will force indexing. 
            }
            catch (FunctionIndexingException e)
            {
                Assert.Equal("ErrorProgram.Func", e.MethodName);
            }
        }

        // Various flavors that all bind down to an IAsyncCollector 
        public class Functions
        {
            public static async Task SendDirectClient(
                [FakeQueue("CustomConstructor", CustomPolicy = "Custom")] FakeQueueClient client)
            {
                await client.AddAsync(new FakeQueueData { Message = "abc", ExtraPropertery = "def" });
            }

            public static void SendDontQueue([FakeQueue] out Payload output)
            {
                output = null;
            }
            public static void SendArrayNull([FakeQueue] out string[] outputs)
            {
                outputs = null; // don't queue
            }

            public static void SendArrayLen0([FakeQueue] out string[] outputs)
            {
                outputs = new string[0]; // don't queue
            }

            public static void SendOnePoco([FakeQueue] out Payload output)
            {
                output = new Payload { val1 = 123 };
            }

            // Send a Poco type with a direct conversion to the native type. 
            // This skips JSON serialization because the IConverterManager has a direct conversion. 
            public static void SendOneOtherNative([FakeQueue] out OtherFakeQueueData output)
            {
                output = new OtherFakeQueueData
                {
                    _test = "direct"
                };
            }

            public static void SendOneDerivedNative([FakeQueue] out DerivedFakeQueueData output)
            {
                output = new DerivedFakeQueueData
                {
                    ExtraPropertery = "extra",
                    Message = "message",
                    Bonus = "Bonus!"
                };
            }

            public static void SendOneNative([FakeQueue] out FakeQueueData output)
            {
                output = new FakeQueueData
                {
                    ExtraPropertery = "extra",
                    Message = "message"
                };
            }

            public static void SendOneString([FakeQueue] out string output)
            {
                output = "stringvalue";
            }

            public static void SendArrayString([FakeQueue] out string[] outputs)
            {
                outputs = new string[] {
                    "first", "second"
                };
            }

            public static void SendSyncCollectorBytes([FakeQueue] ICollector<byte[]> collector)
            {
                byte[] first = Encoding.UTF8.GetBytes("first");
                collector.Add(first);

                byte[] second = Encoding.UTF8.GetBytes("second");
                collector.Add(second);
            }

            public static void SendSyncCollectorString([FakeQueue] ICollector<string> collector)
            {
                collector.Add("first");
                collector.Add("second");
            }

            public static void SendAsyncCollectorString([FakeQueue] IAsyncCollector<string> collector)
            {
                collector.AddAsync("first").Wait();
                collector.AddAsync("second").Wait();
            }

            public static void SendCollectorNative([FakeQueue] ICollector<FakeQueueData> collector)
            {
                collector.Add(new FakeQueueData { Message = "first" });
                collector.Add(new FakeQueueData { Message = "second" });
            }

            public static void SendCollectorPoco([FakeQueue] ICollector<Payload> collector)
            {
                collector.Add(new Payload { val1 = 100 });
                collector.Add(new Payload { val1 = 200 });
            }

            public static void SendArrayPoco([FakeQueue] out Payload[] array)
            {
                array = new Payload[] {
                    new Payload { val1 = 100 },
                    new Payload { val1 = 200 }
                };
            }
        }

        [Fact]
        public async Task Test()
        {
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<Functions>(b=>
                {
                    b.AddExtension<FakeQueueClient>();
                })
                .Build();

            JobHost jobHost = host.GetJobHost();
            FakeQueueClient client = host.GetExtension<FakeQueueClient>();

            var p7 = await InvokeAsync(jobHost, client, "SendDirectClient");
            Assert.Single(p7);
            Assert.Equal("abc", p7[0].Message);
            Assert.Equal("def", p7[0].ExtraPropertery);

            var p8 = await InvokeAsync(jobHost, client, "SendOneDerivedNative");
            Assert.Single(p8);
            DerivedFakeQueueData pd8 = (DerivedFakeQueueData)p8[0];
            Assert.Equal("Bonus!", pd8.Bonus); // verify derived prop that wouldn't serialize. 

            var p9 = await InvokeAsync(jobHost, client, "SendOneOtherNative");
            Assert.Single(p9);
            Assert.Equal("direct", p9[0].ExtraPropertery); // Set by the  DirectFakeQueueData.ToEvent

            // Single items
            var p1 = await InvokeJsonAsync<Payload>(jobHost, client, "SendOnePoco");
            Assert.Single(p1);
            Assert.Equal(123, p1[0].val1);

            var p2 = await InvokeAsync(jobHost, client, "SendOneNative");
            Assert.Single(p2);
            Assert.Equal("message", p2[0].Message);
            Assert.Equal("extra", p2[0].ExtraPropertery);

            var p3 = await InvokeAsync(jobHost, client, "SendOneString");
            Assert.Single(p3);
            Assert.Equal("stringvalue", p3[0].Message);

            foreach (string methodName in new string[] { "SendDontQueue", "SendArrayNull", "SendArrayLen0" })
            {
                var p6 = await InvokeAsync(jobHost, client, methodName);
                Assert.Empty(p6);
            }

            // batching 
            foreach (string methodName in new string[] {
                "SendSyncCollectorBytes", "SendArrayString", "SendSyncCollectorString", "SendAsyncCollectorString", "SendCollectorNative" })
            {
                var p4 = await InvokeAsync(jobHost, client, methodName);
                Assert.Equal(2, p4.Length);
                Assert.Equal("first", p4[0].Message);
                Assert.Equal("second", p4[1].Message);
            }

            foreach (string methodName in new string[] { "SendCollectorPoco", "SendArrayPoco" })
            {
                var p5 = await InvokeJsonAsync<Payload>(jobHost, client, methodName);
                Assert.Equal(2, p5.Length);
                Assert.Equal(100, p5[0].val1);
                Assert.Equal(200, p5[1].val1);
            }
        }

        static async Task<FakeQueueData[]> InvokeAsync(JobHost host, FakeQueueClient client, string name)
        {
            var method = typeof(Functions).GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            await host.CallAsync(method);

            var data = client._items.ToArray();
            client._items.Clear();
            return data;
        }

        static async Task<T[]> InvokeJsonAsync<T>(JobHost host, FakeQueueClient client, string name)
        {
            var method = typeof(Functions).GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            await host.CallAsync(method);

            var data = client._items.ToArray();

            var obj = Array.ConvertAll(data, x => JsonConvert.DeserializeObject<T>(x.Message));
            client._items.Clear();
            return obj;
        }
    }
}
