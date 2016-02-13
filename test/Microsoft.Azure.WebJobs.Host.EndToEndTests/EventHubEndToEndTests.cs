// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Xunit;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    // $$$ Fix this
    public class EventHubEndToEndTests
    {
        class FakeNameResolver : INameResolver
        {
            public IDictionary<string, string> _dict = new  Dictionary<string,string>();
            public string Resolve(string name)
            {
                return _dict[name];
            }
        }
        
        private FakeNameResolver _nameResolver = new FakeNameResolver();

        [Fact]
        public async Task Test()
        {
            await Task.Delay(0); // $$$

            TestTraceWriter trace = new TestTraceWriter(TraceLevel.Info);

            JobHostConfiguration config = new JobHostConfiguration()
            {
                NameResolver = _nameResolver,
                TypeLocator = new FakeTypeLocator(typeof(Functions))
            };

            var eventHubConfig = new EventHubConfiguration();
            string eventHubName = "test89123";
            _nameResolver._dict["eh"] = eventHubName; // bind %eh% in attributes to our hub
            eventHubConfig.AddSender(eventHubName, "Endpoint=sb://test89123-ns.servicebus.windows.net/;SharedAccessKeyName=SendRule;SharedAccessKey=XXXXXXXXXXXXXXX=");
            eventHubConfig.AddReceiver(eventHubName, "Endpoint=sb://test89123-ns.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=YYYYYYYYYYYYYYYY");


            config.Tracing.Tracers.Add(trace);
            config.UseEventHub(eventHubConfig);
            JobHost host = new JobHost(config);

            // Manually invoked. 
            var method = typeof(Functions).GetMethod("SendEvents", BindingFlags.Public | BindingFlags.Static);
            Thread t = new Thread(_ =>
           {
               while (true)
               {
                   host.Call(method);
                   Thread.Sleep(5 * 1000);
               }
           });
            host.Call(method);
            //t.Start();
            host.RunAndBlock();
        }


        public class StressFunctions
        {
            static HashSet<string> _x = new HashSet<string>();
            static int _failCount = 0;
            static int _totalQueueCount = 0;

            public static void Trigger(
                [EventHubTrigger("EHName")] string[] messages)
            {
                foreach (var message in messages)
                {
                    lock(_x)
                    {
                        bool removed = _x.Remove(message);
                        if (!removed)
                        {
                            // Failure!
                            _failCount++;
                        }
                    }
                }
            }

            public static async Task SendEvents(
                [EventHub("EHName")] IAsyncCollector<string> messages)
            {
                // Send some events. 
                for (int i = 0; i < 10; i++)
                {
                    string data = string.Format("{0}|{1}", i, Guid.NewGuid());
                    lock(_x)
                    {
                        _totalQueueCount++;
                        _x.Add(data);
                    }
                    await messages.AddAsync(data);
                }
            }
        } // end stress test 

        public class Functions3
        {
#if false
            // Compare to queue 
            public static void TriggerQueue(
                [QueueTrigger("q123")] Payload message,
                [Queue("q123")] out Payload output)
            {
                // We received new events.                 
                message.val1++;
                output = message;
            }
#else

            // Handle 1 new event
            public static void Trigger8(
                // [EventHubTrigger("%eh%")] Payload message, string prop1, int val1)
                [EventHubTrigger("%eh%")] Payload message,
                string prop1,
                [EventHub("%eh%")] out Payload output)
            {
                bool drain = true;
                output = null;
                if (!drain)
                {
                    // We received new events.                 
                    message.val1++;
                    output = message;
                }
            }       
#endif
        }

        public class TriggerExamples
        {
            // Called when we receive new events
            // Receives a batch at a time. 
            // "partitionContext" is a well-known parameter name, bound to the PartitionContext
            public static void TriggerBatch(
                [EventHubTrigger("%eh%")] EventData[] messages,
                Microsoft.ServiceBus.Messaging.PartitionContext partitionContext)
            {
                // Process a batch of events 
                // Checkpointing & dispose is done when we return from this method        
            }

            public static void TriggerSingle(
               [EventHubTrigger("%eh%")] EventData message)
            {
                // Receive an event at a time 
            }


            public static void TriggerAsString(
                [EventHubTrigger("%eh%")] string message)
            {
                // Receive an event at a time 
            }

            public static void TriggerAsJSon(
                [EventHubTrigger("%eh%")] Payload message)
            {
                // Receive an event at a time, strong bind to JSON 
            }
        }

        public class Functions
        {
            public static void Trigger(
                [QueueTrigger("%eh%")] Payload message,
                string prop1,
                int val1             
                )
            {
                // We received new events.                      
            }

             public static void SendEvents(
                 //[EventHub("EHName")] IAsyncCollector<string> messages,
                 [EventHub("%eh%")] IAsyncCollector<byte[]> message
                 )
            {
                //await Task.Delay(0); // $$$

                // Send some events. 
                //var eventData = new EventData(Encoding.UTF8.GetBytes("test1"));
                //message = new EventData[] { eventData }; // Out binder
                //message = new Payload { prop1 = "MyProp", val1 = 123 };
                // await messages.AddAsync("test1");
                //messages.Add("test1");

                var obj = JsonConvert.SerializeObject(new Payload { prop1 = "p1", val1 = 200 });
                var bytes = Encoding.UTF8.GetBytes( obj);
                message.AddAsync(bytes).Wait();
            }
        }

        public class SendExamples
        {
            public static void Send1Event(
                [EventHub("%eh%")] out Payload message) // single event
            {
                // Queue a single event (if not null)
                message = new Payload { prop1 = "MyProp", val1 = 123 };
            }

            public static async Task SendManyEvents(
                [EventHub("%eh%")] IAsyncCollector<Payload> message) // many events
            {
                for (int i = 0; i < 100; i++)
                {
                    await message.AddAsync(new Payload { prop1 = "MyProp", val1 = i });
                }
            }

            // Queue to multiple diffent hubs. 
            public static void Send3(
                [EventHub("%hub1%")] ICollector<EventData> hub1,  // synchronous!
                [EventHub("%hub2%")] out string[] hub2)
            {
                for (int i = 0; i < 100; i++)
                {
                    var eventData = new EventData(Encoding.UTF8.GetBytes(i.ToString()));
                    hub1.Add(eventData);
                }

                hub2 = new string[]
                    {
                        "first message",
                        "second message"
                    };
            }
        }

            public class Payload
        {
            public string prop1 { get; set; }
            public int val1 { get; set; }
        }
    }
}