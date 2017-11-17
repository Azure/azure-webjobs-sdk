// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Azure.ServiceBus;
using Xunit;
using System.Text;
using Microsoft.Azure.ServiceBus.InteropExtensions; // TODO:
using Microsoft.Azure.ServiceBus.Core;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    public class ServiceBusEndToEndTests
    {
        private const string Prefix = "core-test-";
        private const string FirstQueueName = Prefix + "queue1";
        private const string SecondQueueName = Prefix + "queue2";
        private const string BinderQueueName = Prefix + "queue3";

        private const string TopicName = Prefix + "topic1";
        private const string TopicSubscriptionName1 = "sub1";
        private const string TopicSubscriptionName2 = "sub2";

        private const int SBTimeout = 60 * 1000;

        private static EventWaitHandle _topicSubscriptionCalled1;
        private static EventWaitHandle _topicSubscriptionCalled2;

        // These two variables will be checked at the end of the test
        private static string _resultMessage1;
        private static string _resultMessage2;

        private ServiceBusConfiguration _serviceBusConfig;
        private RandomNameResolver _nameResolver;
        private string _secondaryConnectionString;

        public ServiceBusEndToEndTests()
        {
            _serviceBusConfig = new ServiceBusConfiguration();
            _nameResolver = new RandomNameResolver();
            _secondaryConnectionString = AmbientConnectionStringProvider.Instance.GetConnectionString("ServiceBusSecondary");
        }

        [Fact]
        public async Task ServiceBusEndToEnd()
        {
            try
            {
                await ServiceBusEndToEndInternal(typeof(ServiceBusTestJobs));
            }
            finally
            {
                await Cleanup();
            }
        }

        [Fact]
        public async Task ServiceBusBinderTest()
        {
            try
            {
                var hostType = typeof(ServiceBusTestJobs);
                var host = CreateHost(hostType);
                var method = typeof(ServiceBusTestJobs).GetMethod("ServiceBusBinderTest");

                int numMessages = 10;
                var args = new { message = "Test Message", numMessages = numMessages };
                await host.CallAsync(method, args);
                await host.CallAsync(method, args);
                await host.CallAsync(method, args);

                var count = await CleanUpEntity(BinderQueueName);

                Assert.Equal(numMessages * 3, count);
            } finally
            {
                await Cleanup();
            }
        }

        [Fact]
        public async Task CustomMessageProcessorTest()
        {
            try
            {
                TestTraceWriter trace = new TestTraceWriter(TraceLevel.Info);
                _serviceBusConfig = new ServiceBusConfiguration();
                _serviceBusConfig.MessagingProvider = new CustomMessagingProvider(_serviceBusConfig, trace);

                JobHostConfiguration config = new JobHostConfiguration()
                {
                    NameResolver = _nameResolver,
                    TypeLocator = new FakeTypeLocator(typeof(ServiceBusTestJobs))
                };
                config.Tracing.Tracers.Add(trace);
                config.UseServiceBus(_serviceBusConfig);
                JobHost host = new JobHost(config);

                await ServiceBusEndToEndInternal(typeof(ServiceBusTestJobs), host: host);

                // in addition to verifying that our custom processor was called, we're also
                // verifying here that extensions can log to the TraceWriter
                Assert.Equal(4, trace.Traces.Count(p => p.Message.Contains("Custom processor Begin called!")));
                Assert.Equal(4, trace.Traces.Count(p => p.Message.Contains("Custom processor End called!")));
            }
            finally
            {
                await Cleanup();
            }
        }

        [Fact]
        public async Task MultipleAccountTest()
        {
            try
            {
                TestTraceWriter trace = new TestTraceWriter(TraceLevel.Info);
                _serviceBusConfig = new ServiceBusConfiguration();
                _serviceBusConfig.MessagingProvider = new CustomMessagingProvider(_serviceBusConfig, trace);

                JobHostConfiguration config = new JobHostConfiguration()
                {
                    NameResolver = _nameResolver,
                    TypeLocator = new FakeTypeLocator(typeof(ServiceBusTestJobs))
                };
                config.Tracing.Tracers.Add(trace);
                config.UseServiceBus(_serviceBusConfig);
                JobHost host = new JobHost(config);

                await WriteQueueMessage(_secondaryConnectionString, FirstQueueName, "Test");

                _topicSubscriptionCalled1 = new ManualResetEvent(initialState: false);

                await host.StartAsync();

                _topicSubscriptionCalled1.WaitOne(SBTimeout);

                // ensure all logs have had a chance to flush
                await Task.Delay(3000);

                // Wait for the host to terminate
                await host.StopAsync();
                host.Dispose();

                Assert.Equal("Test-topic-1", _resultMessage1);
            }
            finally
            {
                await Cleanup();
            }
        }

        private async Task<int> CleanUpEntity(string queueName, string connectionString = null)
        {
            var messageReceiver = new MessageReceiver(!string.IsNullOrEmpty(connectionString) ? connectionString : _serviceBusConfig.ConnectionString, queueName, ReceiveMode.ReceiveAndDelete);
            Message message;
            int count = 0;
            do
            {
                message = await messageReceiver.ReceiveAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                if (message != null)
                {
                    count++;
                }
                else
                {
                    break;
                }
            } while (true);
            await messageReceiver.CloseAsync();
            return count;
        }

        private async Task Cleanup()
        {
            await CleanUpEntity(FirstQueueName);
            await CleanUpEntity(SecondQueueName);
            await CleanUpEntity(BinderQueueName);
            await CleanUpEntity(FirstQueueName, _secondaryConnectionString);

            await CleanUpEntity(EntityNameHelper.FormatSubscriptionPath(TopicName, TopicSubscriptionName1));
            await CleanUpEntity(EntityNameHelper.FormatSubscriptionPath(TopicName, TopicSubscriptionName2));
        }

        private JobHost CreateHost(Type jobContainerType)
        {
            JobHostConfiguration config = new JobHostConfiguration()
            {
                NameResolver = _nameResolver,
                TypeLocator = new FakeTypeLocator(jobContainerType)
            };
            config.UseServiceBus(_serviceBusConfig);
            return new JobHost(config);
        }

        private async Task ServiceBusEndToEndInternal(Type jobContainerType, JobHost host = null, bool verifyLogs = true)
        {
            StringWriter consoleOutput = null;
            TextWriter hold = null;
            if (verifyLogs)
            {
                consoleOutput = new StringWriter();
                hold = Console.Out;
                Console.SetOut(consoleOutput);
            }

            if (host == null)
            {
                host = CreateHost(jobContainerType);
            }

            await WriteQueueMessage(_serviceBusConfig.ConnectionString, FirstQueueName, "E2E");

            _topicSubscriptionCalled1 = new ManualResetEvent(initialState: false);
            _topicSubscriptionCalled2 = new ManualResetEvent(initialState: false);

            await host.StartAsync();

            _topicSubscriptionCalled1.WaitOne(SBTimeout);
            _topicSubscriptionCalled2.WaitOne(SBTimeout);

            // ensure all logs have had a chance to flush
            await Task.Delay(3000);

            // Wait for the host to terminate
            await host.StopAsync();
            host.Dispose();

            Assert.Equal("E2E-SBQueue2SBQueue-SBQueue2SBTopic-topic-1", _resultMessage1);
            Assert.Equal("E2E-SBQueue2SBQueue-SBQueue2SBTopic-topic-2", _resultMessage2);

            if (verifyLogs)
            {
                Console.SetOut(hold);

                string[] consoleOutputLines = consoleOutput.ToString().Trim().Split(new string[] { Environment.NewLine }, StringSplitOptions.None).OrderBy(p => p).ToArray();
                string[] expectedOutputLines = new string[]
                {
                    "Found the following functions:",
                    string.Format("{0}.SBQueue2SBQueue", jobContainerType.FullName),
                    string.Format("{0}.MultipleAccounts", jobContainerType.FullName),
                    string.Format("{0}.SBQueue2SBTopic", jobContainerType.FullName),
                    string.Format("{0}.SBTopicListener1", jobContainerType.FullName),
                    string.Format("{0}.SBTopicListener2", jobContainerType.FullName),
                    "Job host started",
                    string.Format("Executing '{0}.SBQueue2SBQueue' (Reason='New ServiceBus message detected on '{1}'.', Id=", jobContainerType.Name, FirstQueueName),
                    string.Format("Executed '{0}.SBQueue2SBQueue' (Succeeded, Id=", jobContainerType.Name),
                    string.Format("Executing '{0}.SBQueue2SBTopic' (Reason='New ServiceBus message detected on '{1}'.', Id=", jobContainerType.Name, SecondQueueName),
                    string.Format("Executed '{0}.SBQueue2SBTopic' (Succeeded, Id=", jobContainerType.Name),
                    string.Format("Executing '{0}.SBTopicListener1' (Reason='New ServiceBus message detected on '{1}'.', Id=", jobContainerType.Name, EntityNameHelper.FormatSubscriptionPath(TopicName, TopicSubscriptionName1)),
                    string.Format("Executed '{0}.SBTopicListener1' (Succeeded, Id=", jobContainerType.Name),
                    string.Format("Executing '{0}.SBTopicListener2' (Reason='New ServiceBus message detected on '{1}'.', Id=", jobContainerType.Name, EntityNameHelper.FormatSubscriptionPath(TopicName, TopicSubscriptionName2)),
                    string.Format("Executed '{0}.SBTopicListener2' (Succeeded, Id=", jobContainerType.Name),
                    "Job host stopped"
                }.OrderBy(p => p).ToArray();

                bool hasError = consoleOutputLines.Any(p => p.Contains("Function had errors"));
                if (!hasError)
                {
                    for (int i = 0; i < expectedOutputLines.Length; i++)
                    {
                        Assert.StartsWith(expectedOutputLines[i], consoleOutputLines[i]);
                    }
                }
            }
        }

        private async Task WriteQueueMessage(string connectionString, string queueName, string message)
        {

            QueueClient queueClient = new QueueClient(connectionString, queueName);
            await queueClient.SendAsync(new Message(Encoding.UTF8.GetBytes(message)));
            await queueClient.CloseAsync();
        }

        public abstract class ServiceBusTestJobsBase
        {
            protected static string SBQueue2SBQueue_GetOutputMessage(string input)
            {
                return input + "-SBQueue2SBQueue";
            }

            protected static Message SBQueue2SBTopic_GetOutputMessage(string input)
            {
                input = input + "-SBQueue2SBTopic";

                var output = new Message(Encoding.UTF8.GetBytes(input));
                output.ContentType = "text/plain";

                return output;
            }

            protected static void SBTopicListener1Impl(string input)
            {
                _resultMessage1 = input + "-topic-1";
                _topicSubscriptionCalled1.Set();
            }

            protected static void SBTopicListener2Impl(Message message)
            {
                using (Stream stream = new MemoryStream(message.Body))
                using (TextReader reader = new StreamReader(stream))
                {
                    _resultMessage2 = reader.ReadToEnd() + "-topic-2";
                }

                _topicSubscriptionCalled2.Set();
            }
        }

        public class ServiceBusTestJobs : ServiceBusTestJobsBase
        {
            // Passes service bus message from a queue to another queue
            public static void SBQueue2SBQueue(
                [ServiceBusTrigger(FirstQueueName)] string start, int deliveryCount,
                [ServiceBus(SecondQueueName)] out string message)
            {
                Assert.Equal(1, deliveryCount);
                message = SBQueue2SBQueue_GetOutputMessage(start);
            }

            // Passes a service bus message from a queue to topic using a brokered message
            public static void SBQueue2SBTopic(
                [ServiceBusTrigger(SecondQueueName)] string message,
                [ServiceBus(TopicName)] out Message output)
            {
                output = SBQueue2SBTopic_GetOutputMessage(message);
            }

            // First listener for the topic
            public static void SBTopicListener1(
                [ServiceBusTrigger(TopicName, TopicSubscriptionName1)] string message)
            {
                SBTopicListener1Impl(message);
            }

            // Second listener for the topic
            // Just sprinkling Singleton here because previously we had a bug where this didn't work
            // for ServiceBus.
            [Singleton]
            public static void SBTopicListener2(
                [ServiceBusTrigger(TopicName, TopicSubscriptionName2)] Message message)
            {
                SBTopicListener2Impl(message);
            }

            // Demonstrate triggering on a queue in one account, and writing to a topic
            // in the primary subscription
            public static void MultipleAccounts(
                [ServiceBusTrigger(FirstQueueName, Connection = "ServiceBusSecondary")] string input,
                [ServiceBus(TopicName)] out string output)
            {
                output = input;
            }

            [NoAutomaticTrigger]
            public static async Task ServiceBusBinderTest(
                string message,
                int numMessages,
                Binder binder)
            {
                var attribute = new ServiceBusAttribute(BinderQueueName)
                {
                    EntityType = EntityType.Queue
                };
                var collector = await binder.BindAsync<IAsyncCollector<string>>(attribute);

                for (int i = 0; i < numMessages; i++)
                {
                    await collector.AddAsync(message + i);
                }

                await collector.FlushAsync();
            }
        }

        private class CustomMessagingProvider : MessagingProvider
        {
            private readonly ServiceBusConfiguration _config;
            private readonly TraceWriter _trace;

            public CustomMessagingProvider(ServiceBusConfiguration config, TraceWriter trace)
                : base(config)
            {
                _config = config;
                _trace = trace;
            }

            public override MessageProcessor CreateMessageProcessor(string entityPath, string connectionName = null)
            {
                var options = new MessageHandlerOptions(ExceptionReceivedHandler)
                {
                    MaxConcurrentCalls = 3,
                    MaxAutoRenewDuration = TimeSpan.FromMinutes(1)
                };

                var messageReceiver = new MessageReceiver(_config.ConnectionString, entityPath);

                return new CustomMessageProcessor(messageReceiver, options, _trace);
            }

            private class CustomMessageProcessor : MessageProcessor
            {
                private readonly TraceWriter _trace;

                public CustomMessageProcessor(MessageReceiver messageReceiver, MessageHandlerOptions messageOptions, TraceWriter trace)
                    : base(messageReceiver, messageOptions)
                {
                    _trace = trace;
                }

                public override async Task<bool> BeginProcessingMessageAsync(Message message, CancellationToken cancellationToken)
                {
                    _trace.Info("Custom processor Begin called!");
                    return await base.BeginProcessingMessageAsync(message, cancellationToken);
                }

                public override async Task CompleteProcessingMessageAsync(Message message, Executors.FunctionResult result, CancellationToken cancellationToken)
                {
                    _trace.Info("Custom processor End called!");
                    await base.CompleteProcessingMessageAsync(message, result, cancellationToken);
                }
            }

            Task ExceptionReceivedHandler(ExceptionReceivedEventArgs eventArgs)
            {
                return Task.CompletedTask;
            }
        }
    }
}