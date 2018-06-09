// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

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

        private RandomNameResolver _nameResolver;
        private string _primaryConnectionString;
        private string _secondaryConnectionString;

        public ServiceBusEndToEndTests()
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            var connStrProvider = new AmbientConnectionStringProvider(config);
            _primaryConnectionString = connStrProvider.GetConnectionString(ConnectionStringNames.ServiceBus);
            _secondaryConnectionString = connStrProvider.GetConnectionString("ServiceBusSecondary");

            _nameResolver = new RandomNameResolver();

            Cleanup().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task ServiceBusEndToEnd()
        {
            try
            {
                await ServiceBusEndToEndInternal();
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
                var host = CreateHost();
                var method = typeof(ServiceBusTestJobs).GetMethod("ServiceBusBinderTest");

                int numMessages = 10;
                var args = new { message = "Test Message", numMessages = numMessages };
                var jobHost = host.GetJobHost<ServiceBusTestJobs>();
                await jobHost.CallAsync(method, args);
                await jobHost.CallAsync(method, args);
                await jobHost.CallAsync(method, args);

                var count = await CleanUpEntity(BinderQueueName);

                Assert.Equal(numMessages * 3, count);
            }
            finally
            {
                await Cleanup();
            }
        }

        [Fact]
        public async Task CustomMessageProcessorTest()
        {
            try
            {
                IHost host = new HostBuilder()
                    .ConfigureDefaultTestHost<ServiceBusTestJobs>()
                    .AddServiceBus()
                    .ConfigureServices(services =>
                    {
                        services.AddSingleton<MessagingProvider, CustomMessagingProvider>();
                    })
                    .Build();

                var loggerProvider = host.GetTestLoggerProvider();

                await ServiceBusEndToEndInternal(host: host);

                // in addition to verifying that our custom processor was called, we're also
                // verifying here that extensions can log
                IEnumerable<LogMessage> messages = loggerProvider.GetAllLogMessages().Where(m => m.Category == CustomMessagingProvider.CustomMessagingCategory);
                Assert.Equal(4, messages.Count(p => p.FormattedMessage.Contains("Custom processor Begin called!")));
                Assert.Equal(4, messages.Count(p => p.FormattedMessage.Contains("Custom processor End called!")));
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
                IHost host = new HostBuilder()
                   .ConfigureDefaultTestHost<ServiceBusTestJobs>(nameResolver: _nameResolver)
                   .AddServiceBus()
                   .ConfigureServices(services =>
                   {
                       services.AddSingleton<MessagingProvider, CustomMessagingProvider>();
                   })
                   .Build();

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
            var messageReceiver = new MessageReceiver(!string.IsNullOrEmpty(connectionString) ? connectionString : _primaryConnectionString, queueName, ReceiveMode.ReceiveAndDelete);
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

        private IHost CreateHost()
        {
            return new HostBuilder()
                .ConfigureDefaultTestHost<ServiceBusTestJobs>()
                .AddServiceBus()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<INameResolver>(_nameResolver);
                })
                .Build();
        }

        private async Task ServiceBusEndToEndInternal(IHost host = null)
        {
            if (host == null)
            {
                host = CreateHost();
            }

            var jobContainerType = typeof(ServiceBusTestJobs);

            await WriteQueueMessage(_primaryConnectionString, FirstQueueName, "E2E");

            _topicSubscriptionCalled1 = new ManualResetEvent(initialState: false);
            _topicSubscriptionCalled2 = new ManualResetEvent(initialState: false);

            using (host)
            {
                await host.StartAsync();

                _topicSubscriptionCalled1.WaitOne(SBTimeout);
                _topicSubscriptionCalled2.WaitOne(SBTimeout);

                // ensure all logs have had a chance to flush
                await Task.Delay(3000);

                // Wait for the host to terminate
                await host.StopAsync();

                Assert.Equal("E2E-SBQueue2SBQueue-SBQueue2SBTopic-topic-1", _resultMessage1);
                Assert.Equal("E2E-SBQueue2SBQueue-SBQueue2SBTopic-topic-2", _resultMessage2);

                // filter out anything from the custom processor for easier validation.
                IEnumerable<LogMessage> consoleOutput = host.GetTestLoggerProvider()
                    .GetAllLogMessages()
                    .Where(m => m.Category != CustomMessagingProvider.CustomMessagingCategory);

                Assert.DoesNotContain(consoleOutput, p => p.Level == LogLevel.Error);

                string[] consoleOutputLines = consoleOutput
                    .Where(p => p.FormattedMessage != null)
                    .SelectMany(p => p.FormattedMessage.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
                    .OrderBy(p => p)
                    .ToArray();

                string[] expectedOutputLines = new string[]
                {
                   "Found the following functions:",
                    $"{jobContainerType.FullName}.SBQueue2SBQueue",
                    $"{jobContainerType.FullName}.MultipleAccounts",
                    $"{jobContainerType.FullName}.SBQueue2SBTopic",
                    $"{jobContainerType.FullName}.SBTopicListener1",
                    $"{jobContainerType.FullName}.SBTopicListener2",
                    $"{jobContainerType.FullName}.ServiceBusBinderTest",
                    "Job host started",
                    $"Executing '{jobContainerType.Name}.SBQueue2SBQueue' (Reason='New ServiceBus message detected on '{FirstQueueName}'.', Id=",
                    $"Executed '{jobContainerType.Name}.SBQueue2SBQueue' (Succeeded, Id=",
                    $"Executing '{jobContainerType.Name}.SBQueue2SBTopic' (Reason='New ServiceBus message detected on '{SecondQueueName}'.', Id=",
                    $"Executed '{jobContainerType.Name}.SBQueue2SBTopic' (Succeeded, Id=",
                    $"Executing '{jobContainerType.Name}.SBTopicListener1' (Reason='New ServiceBus message detected on '{EntityNameHelper.FormatSubscriptionPath(TopicName, TopicSubscriptionName1)}'.', Id=",
                    $"Executed '{jobContainerType.Name}.SBTopicListener1' (Succeeded, Id=",
                    $"Executing '{jobContainerType.Name}.SBTopicListener2' (Reason='New ServiceBus message detected on '{EntityNameHelper.FormatSubscriptionPath(TopicName, TopicSubscriptionName2)}'.', Id=",
                    $"Executed '{jobContainerType.Name}.SBTopicListener2' (Succeeded, Id=",
                    "Job host stopped",
                    "Starting JobHost",
                    "Stopping JobHost"
                }.OrderBy(p => p).ToArray();

                Action<string>[] inspectors = expectedOutputLines.Select<string, Action<string>>(p => (string m) => m.StartsWith(p)).ToArray();
                Assert.Collection(consoleOutputLines, inspectors);
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
            protected static Message SBQueue2SBQueue_GetOutputMessage(string input)
            {
                input = input + "-SBQueue2SBQueue";
                return new Message
                {
                    ContentType = "text/plain",
                    Body = Encoding.UTF8.GetBytes(input)
                };
            }

            protected static Message SBQueue2SBTopic_GetOutputMessage(string input)
            {
                input = input + "-SBQueue2SBTopic";

                return new Message(Encoding.UTF8.GetBytes(input))
                {
                    ContentType = "text/plain"
                };
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
            public static async Task SBQueue2SBQueue(
                [ServiceBusTrigger(FirstQueueName)] string start, int deliveryCount,
                MessageReceiver messageReceiver,
                string lockToken,
                [ServiceBus(SecondQueueName)] MessageSender messageSender)
            {
                Assert.Equal(FirstQueueName, messageReceiver.Path);
                Assert.Equal(1, deliveryCount);

                // verify the message receiver and token are valid
                await messageReceiver.RenewLockAsync(lockToken);

                var message = SBQueue2SBQueue_GetOutputMessage(start);
                await messageSender.SendAsync(message);
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
                [ServiceBusTrigger(TopicName, TopicSubscriptionName1)] string message,
                MessageReceiver messageReceiver,
                string lockToken)
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
            public const string CustomMessagingCategory = "CustomMessagingProvider";
            private readonly ILogger _logger;
            private readonly ServiceBusOptions _options;

            public CustomMessagingProvider(IOptions<ServiceBusOptions> serviceBusOptions, ILoggerFactory loggerFactory)
                : base(serviceBusOptions)
            {
                _options = serviceBusOptions.Value;
                _logger = loggerFactory?.CreateLogger(CustomMessagingCategory);
            }

            public override MessageProcessor CreateMessageProcessor(string entityPath, string connectionName = null)
            {
                var options = new MessageHandlerOptions(ExceptionReceivedHandler)
                {
                    MaxConcurrentCalls = 3,
                    MaxAutoRenewDuration = TimeSpan.FromMinutes(1)
                };

                var messageReceiver = new MessageReceiver(_options.ConnectionString, entityPath);

                return new CustomMessageProcessor(messageReceiver, options, _logger);
            }

            private class CustomMessageProcessor : MessageProcessor
            {
                private readonly ILogger _logger;

                public CustomMessageProcessor(MessageReceiver messageReceiver, MessageHandlerOptions messageOptions, ILogger logger)
                    : base(messageReceiver, messageOptions)
                {
                    _logger = logger;
                }

                public override async Task<bool> BeginProcessingMessageAsync(Message message, CancellationToken cancellationToken)
                {
                    _logger?.LogInformation("Custom processor Begin called!");
                    return await base.BeginProcessingMessageAsync(message, cancellationToken);
                }

                public override async Task CompleteProcessingMessageAsync(Message message, Executors.FunctionResult result, CancellationToken cancellationToken)
                {
                    _logger?.LogInformation("Custom processor End called!");
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