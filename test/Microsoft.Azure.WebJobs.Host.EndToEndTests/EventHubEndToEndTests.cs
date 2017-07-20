// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Xunit;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    public class EventHubEndToEndTests : IDisposable
    {
        private JobHost _host;
        private JobHostConfiguration _config;
        private const string TestHubName = "webjobstesthub";
        private const string TestHub2Name = "webjobstesthub2";
        private const string TestHub2Connection = "AzureWebJobsTestHubConnection2";

        public void SetupUnorderedEventListenerConfig()
        {
            var config = new JobHostConfiguration()
            {
                TypeLocator = new FakeTypeLocator(typeof(EventHubTestJobs))
            };
            var eventHubConfig = new EventHubConfiguration();
            TestTraceWriter trace = new TestTraceWriter(TraceLevel.Info);

            string connection = Environment.GetEnvironmentVariable("AzureWebJobsTestHubConnection");
            Assert.True(!string.IsNullOrEmpty(connection), "Required test connection string is missing.");
            eventHubConfig.AddSender(TestHubName, connection);
            eventHubConfig.AddReceiver(TestHubName, connection);

            connection = Environment.GetEnvironmentVariable(TestHub2Connection);
            Assert.True(!string.IsNullOrEmpty(connection), "Required test connection string is missing.");

            config.Tracing.Tracers.Add(trace);
            config.UseEventHub(eventHubConfig);
            _config = config;
            _host = new JobHost(config);

            EventHubTestJobs.Result = null;
        }

        public void SetupOrderedEventListenerConfig()
        {
            var config = new JobHostConfiguration()
            {
                TypeLocator = new FakeTypeLocator(typeof(EventHubTestJobs))
            };
            var eventHubConfig = new EventHubConfiguration()
            {
                PartitionKeyOrdering = true
            };

            TestTraceWriter trace = new TestTraceWriter(TraceLevel.Info);

            string connection = Environment.GetEnvironmentVariable("AzureWebJobsTestHubConnection");
            Assert.True(!string.IsNullOrEmpty(connection), "Required test connection string is missing.");
            eventHubConfig.AddSender(TestHubName, connection);
            eventHubConfig.AddReceiver(TestHubName, connection);

            connection = Environment.GetEnvironmentVariable(TestHub2Connection);
            Assert.True(!string.IsNullOrEmpty(connection), "Required test connection string is missing.");

            config.Tracing.Tracers.Add(trace);
            config.UseEventHub(eventHubConfig);
            _config = config;
            _host = new JobHost(config);

            EventHubTestJobs.Result = null;
        }



        [Fact]
        public async Task EventHubTriggerTest_UnorderedListener_SingleDispatch()
        {
            SetupUnorderedEventListenerConfig();
            await _host.StartAsync();

            try
            {
                var method = typeof(EventHubTestJobs).GetMethod("SendEvent_TestHub", BindingFlags.Static | BindingFlags.Public);
                var id = Guid.NewGuid().ToString();
                EventHubTestJobs.EventId = id;
                await _host.CallAsync(method, new { input = id });

                await TestHelpers.Await(() =>
                {
                    return EventHubTestJobs.Result != null;
                });

                Assert.Equal(id, (object)EventHubTestJobs.Result);
            }
            finally
            {
                await _host.StopAsync();
                AssertDispatcherLogEntries(false, null, null, true, 1);
            }
        }

        [Fact]
        public async Task EventHubTriggerTest_OrderedListener_SingleDispatch()
        {
            SetupOrderedEventListenerConfig();
            await _host.StartAsync();

            try
            {
                var method = typeof(EventHubTestJobs).GetMethod("SendEvent_TestHub", BindingFlags.Static | BindingFlags.Public);
                var id = Guid.NewGuid().ToString();
                EventHubTestJobs.EventId = id;
                await _host.CallAsync(method, new { input = id });

                await TestHelpers.Await(() =>
                {
                    return EventHubTestJobs.Result != null;
                });

                Assert.Equal(id, (object)EventHubTestJobs.Result);
            }
            finally
            {
                await _host.StopAsync();
                AssertDispatcherLogEntries(true, "4", "64", true, 1);
            }
        }

        [Fact]
        public async Task EventHubTriggerTest_UnorderedListener_MultipleDispatch()
        {
            // send some events BEFORE starting the host, to ensure
            // the events are received in batch
            SetupUnorderedEventListenerConfig();
            var method = typeof(EventHubTestJobs).GetMethod("SendEvents_TestHub2", BindingFlags.Static | BindingFlags.Public);
            var id = Guid.NewGuid().ToString();
            EventHubTestJobs.EventId = id;
            int numEvents = 5;
            await _host.CallAsync(method, new { numEvents = numEvents, input = id });

            try
            {
                await _host.StartAsync();

                await TestHelpers.Await(() =>
                {
                    return EventHubTestJobs.Result != null;
                });

                var eventsProcessed = (string[])EventHubTestJobs.Result;
                Assert.True(eventsProcessed.Length == numEvents);
            }
            finally
            {
                await _host.StopAsync();
                AssertDispatcherLogEntries(false, null, null, false, numEvents);
            }
        }

        [Fact]
        public async Task EventHubTriggerTest_OrderedListener_MultipleDispatch()
        {
            // send some events BEFORE starting the host, to ensure
            // the events are received in batch
            SetupOrderedEventListenerConfig();
            var method = typeof(EventHubTestJobs).GetMethod("SendEvents_TestHub2", BindingFlags.Static | BindingFlags.Public);
            var id = Guid.NewGuid().ToString();
            EventHubTestJobs.EventId = id;
            int numEvents = 5;
            await _host.CallAsync(method, new { numEvents = numEvents, input = id });

            try
            {
                await _host.StartAsync();

                await TestHelpers.Await(() =>
                {
                    return EventHubTestJobs.Result != null;
                });

                var eventsProcessed = (string[])EventHubTestJobs.Result;
                Assert.True(eventsProcessed.Length == numEvents);
            }
            finally
            {
                await _host.StopAsync();
                AssertDispatcherLogEntries(true, "4", "64", false, numEvents);
            }
        }

        [Fact]
        public async Task EventHubTriggerTest_OrderedListener_MultipleDispatch_DefaultSlotCount()
        {
            // send some events BEFORE starting the host, to ensure
            // the events are received in batch
            SetupOrderedEventListenerConfig();

            var method = typeof(EventHubTestJobs).GetMethod("SendEvents_TestHub3", BindingFlags.Static | BindingFlags.Public);

            int numEventsPerPartitionKey = 2;
            int partitionCount = 4;
            int numEvents = numEventsPerPartitionKey * partitionCount;
            var id = Guid.NewGuid().ToString();
            EventHubTestJobs.EventId = id;

            for (int i = 0; i < numEventsPerPartitionKey; i++)
            {
                for (int j = 0; j < partitionCount; j++)
                {
                    await _host.CallAsync(method, new { numEvents = numEventsPerPartitionKey, partitionId = j, input = id });
                }
            }

            try
            {
                await _host.StartAsync();

                await TestHelpers.Await(() =>
                {
                    return EventHubTestJobs.Result != null;
                });

                var eventsProcessed = (string[])EventHubTestJobs.Result;
                Assert.True(eventsProcessed.Length >= 1);
            }
            finally
            {
                await _host.StopAsync();
                AssertDispatcherLogEntries(true, "4", "64", false, numEvents/2);
            }
        }

        public void Dispose()
        {
            _host?.Dispose();
        }

        public static class EventHubTestJobs
        {
            public static string EventId;

            public static object Result { get; set; }

            public static void SendEvent_TestHub(string input, [EventHub(TestHubName)] out EventData evt)
            {
                evt = new EventData(Encoding.UTF8.GetBytes(input))
                {
                    PartitionKey = "TestPartition"
                };
                evt.Properties.Add("TestProp1", "value1");
                evt.Properties.Add("TestProp2", "value2");
            }

            public static void SendEvents_TestHub2(int numEvents, string input, [EventHub(TestHub2Name, Connection = TestHub2Connection)] out EventData[] events)
            {
                events = new EventData[numEvents];
                for (int i = 0; i < numEvents; i++)
                {
                    var evt = new EventData(Encoding.UTF8.GetBytes(input));
                    evt.PartitionKey = "TestPartition";
                    evt.Properties.Add("TestIndex", i);
                    evt.Properties.Add("TestProp1", "value1");
                    evt.Properties.Add("TestProp2", "value2");
                    events[i] = evt;
                }
            }

            public static void SendEvents_TestHub3(string input, int partitionId, [EventHub(TestHub2Name, Connection = TestHub2Connection)] out EventData evt)
            {
                evt = new EventData(Encoding.UTF8.GetBytes(input))
                {
                    PartitionKey = "TestPartition" + partitionId.ToString()
                };

                evt.Properties.Add("TestIndex", partitionId);
                evt.Properties.Add("TestProp1", "value1");
                evt.Properties.Add("TestProp2", "value2");
            }


            public static void ProcessSingleEvent([EventHubTrigger(TestHubName)] string evt, 
                string partitionKey, DateTime enqueuedTimeUtc, IDictionary<string, object> properties,
                IDictionary<string, object> systemProperties)
            {
                // filter for the ID the current test is using
                if (evt == EventId)
                {
                    Assert.Equal("TestPartition", partitionKey);
                    Assert.True((DateTime.Now - enqueuedTimeUtc).TotalSeconds < 30);

                    Assert.Equal(2, properties.Count);
                    Assert.Equal("value1", properties["TestProp1"]);
                    Assert.Equal("value2", properties["TestProp2"]);

                    Assert.Equal(8, systemProperties.Count);

                    Result = evt;
                }
            }

            public static void ProcessMultipleEvents([EventHubTrigger(TestHub2Name, Connection = TestHub2Connection)] string[] events,
                string[] partitionKeyArray, DateTime[] enqueuedTimeUtcArray, IDictionary<string, object>[] propertiesArray,
                IDictionary<string, object>[] systemPropertiesArray)
            {
                Assert.Equal(events.Length, partitionKeyArray.Length);
                Assert.Equal(events.Length, enqueuedTimeUtcArray.Length);
                Assert.Equal(events.Length, propertiesArray.Length);
                Assert.Equal(events.Length, systemPropertiesArray.Length);

                for (int i = 0; i < events.Length; i++)
                {
                    string partitionKeyNumber = GetPartitionKeyValue(partitionKeyArray[i]);
                    if (partitionKeyNumber != null)
                    {
                        Assert.Equal("TestPartition"+ partitionKeyNumber, partitionKeyArray[i]);
                        Assert.Equal(Convert.ToInt32(partitionKeyNumber), propertiesArray[i]["TestIndex"]);
                    }
                    else
                    {
                        Assert.Equal("TestPartition", partitionKeyArray[i]);
                        Assert.Equal(i, propertiesArray[i]["TestIndex"]);
                    }

                    Assert.Equal(3, propertiesArray[i].Count);
                    Assert.Equal(8, systemPropertiesArray[i].Count);
                }

                // filter for the ID the current test is using
                if (events[0] == EventId)
                {
                    Result = events;
                }
            }

            private static string GetPartitionKeyValue(string partitionKey)
            {
                Regex regex = new Regex(@"(\d+)$",
                                        RegexOptions.Compiled |
                                        RegexOptions.CultureInvariant);

                Match match = regex.Match(partitionKey);

                if(match.Success)
                {
                    return match.Groups[1].Value;
                }

                return null;
            }
        }

        private void AssertDispatcherLogEntries(bool isOrderedEventListener, string maxDop, string boundedCapacity,
                bool isSingleDispatch, int eventCount)
        {
            var tracers = _config.Tracing.Tracers.ToList();
            var traceWriters = tracers.ToList();
            var tracer = traceWriters[0] as TestTraceWriter;

            string dispatchMode = isSingleDispatch ? "Single" : "Batch";

            if (isOrderedEventListener)
            {
                Assert.True(tracer.Traces.Any(m => m.Message.Contains($"Event hub ordered listener: Max degree of parallelism:{maxDop}, bounded capacity:{boundedCapacity}")));
                Assert.True(tracer.Traces.Any(m => m.Message.Contains($"Event hub ordered listener: {dispatchMode} dispatch: Dispatched {eventCount} messages.")));
                Assert.True(tracer.Traces.Any(m => m.Message.Contains($"Event hub ordered listener: Checkpointed {eventCount} messages.")));
            }
            else
            {
                Assert.True(tracer.Traces.Any(m => m.Message.Contains($"Event hub unordered listener: {dispatchMode} dispatch: Dispatched {eventCount} messages.")));
                Assert.True(tracer.Traces.Any(m => m.Message.Contains($"Event hub unordered listener: Checkpointed {eventCount} messages.")));
            }
        }
    }
}
