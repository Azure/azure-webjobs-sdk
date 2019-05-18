// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
    public class ServiceBusSessionsBusEndToEndTests : IDisposable
    {
        private const string _prefix = "core-test-";
        private const string _queueName = _prefix + "queue1-sessions";
        private const string _topicName = _prefix + "topic1-sessions";
        private const string _subscriptionName = "sub1-sessions";
        private static EventWaitHandle _waitHandle1;
        private static EventWaitHandle _waitHandle2;
        private readonly RandomNameResolver _nameResolver;
        private const int SBTimeout = 120 * 1000;
        private readonly string _connectionString;

        public ServiceBusSessionsBusEndToEndTests()
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddTestSettings()
                .Build();

            _connectionString = config.GetConnectionStringOrSetting(ServiceBus.Constants.DefaultConnectionStringName);

            _nameResolver = new RandomNameResolver();

            Cleanup().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task ServiceBusSessionQueue_OrderGuaranteed()
        {
            using (var host = ServiceBusSessionsTestHelper.CreateHost<ServiceBusSessionsTestJobs1>(_nameResolver))
            {
                await host.StartAsync();

                _waitHandle1 = new ManualResetEvent(initialState: false);

                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message1", "test-session1");
                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message2", "test-session1");
                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message3", "test-session1");
                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message4", "test-session1");
                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message5", "test-session1");

                _waitHandle1.WaitOne(SBTimeout);

                IEnumerable<LogMessage> logMessages = host.GetTestLoggerProvider().GetAllLogMessages();

                // filter out anything from the custom processor for easier validation.
                List<LogMessage> consoleOutput = logMessages.Where(m => m.Category == "Function.SBQueue1Trigger.User").ToList();

                Assert.True(consoleOutput.Count() == 5, ServiceBusSessionsTestHelper.GetLogsAsString(consoleOutput));

                int i = 1;
                foreach (LogMessage logMessage in consoleOutput)
                {
                    Assert.True(logMessage.FormattedMessage.StartsWith("message" + i++));
                }
            }
        }

        [Fact]
        public async Task ServiceBusSessionTopicSubscription_OrderGuaranteed()
        {
            using (var host = ServiceBusSessionsTestHelper.CreateHost<ServiceBusSessionsTestJobs1>(_nameResolver))
            {
                await host.StartAsync();

                _waitHandle1 = new ManualResetEvent(initialState: false);

                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message1", "test-session1");
                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message2", "test-session1");
                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message3", "test-session1");
                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message4", "test-session1");
                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message5", "test-session1");

                _waitHandle1.WaitOne(SBTimeout);

                IEnumerable<LogMessage> logMessages = host.GetTestLoggerProvider().GetAllLogMessages();

                // filter out anything from the custom processor for easier validation.
                List<LogMessage> consoleOutput = logMessages.Where(m => m.Category == "Function.SBSub1Trigger.User").ToList();

                Assert.True(consoleOutput.Count() == 5, ServiceBusSessionsTestHelper.GetLogsAsString(consoleOutput));

                int i = 1;
                foreach (LogMessage logMessage in consoleOutput)
                {
                    Assert.True(logMessage.FormattedMessage.StartsWith("message" + i++));
                }
            }
        }

        [Fact]
        public async Task ServiceBusSessionQueue_DifferentHosts_DifferentSessions()
        {

            using (var host1 = ServiceBusSessionsTestHelper.CreateHost<ServiceBusSessionsTestJobs1>(_nameResolver, true))
            using (var host2 = ServiceBusSessionsTestHelper.CreateHost<ServiceBusSessionsTestJobs2>(_nameResolver, true))
            {
                await host1.StartAsync();
                await host2.StartAsync();

                _waitHandle1 = new ManualResetEvent(initialState: false);
                _waitHandle2 = new ManualResetEvent(initialState: false);

                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message1", "test-session1");
                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message1", "test-session2");

                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message2", "test-session1");
                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message2", "test-session2");

                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message3", "test-session1");
                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message3", "test-session2");

                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message4", "test-session1");
                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message4", "test-session2");

                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message5", "test-session1");
                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message5", "test-session2");

                _waitHandle1.WaitOne(SBTimeout);
                _waitHandle2.WaitOne(SBTimeout);

                IEnumerable<LogMessage> logMessages1 = host1.GetTestLoggerProvider().GetAllLogMessages();
                List<LogMessage> consoleOutput1 = logMessages1.Where(m => m.Category == "Function.SBQueue1Trigger.User").ToList();
                Assert.NotEmpty(logMessages1.Where(m => m.Category == "CustomMessagingProvider" && m.FormattedMessage.StartsWith("Custom processor Begin called!")));
                Assert.NotEmpty(logMessages1.Where(m => m.Category == "CustomMessagingProvider" && m.FormattedMessage.StartsWith("Custom processor End called!")));
                IEnumerable<LogMessage> logMessages2 = host2.GetTestLoggerProvider().GetAllLogMessages();
                List<LogMessage> consoleOutput2 = logMessages2.Where(m => m.Category == "Function.SBQueue2Trigger.User").ToList();
                Assert.NotEmpty(logMessages2.Where(m => m.Category == "CustomMessagingProvider" && m.FormattedMessage.StartsWith("Custom processor Begin called!")));
                Assert.NotEmpty(logMessages2.Where(m => m.Category == "CustomMessagingProvider" && m.FormattedMessage.StartsWith("Custom processor End called!")));
                char sessionId1 = consoleOutput1[0].FormattedMessage[consoleOutput1[0].FormattedMessage.Length - 1];
                foreach (LogMessage m in consoleOutput1)
                {
                    Assert.Equal(sessionId1, m.FormattedMessage[m.FormattedMessage.Length - 1]);
                }

                char sessionId2 = consoleOutput2[0].FormattedMessage[consoleOutput1[0].FormattedMessage.Length - 1];
                foreach (LogMessage m in consoleOutput2)
                {
                    Assert.Equal(sessionId2, m.FormattedMessage[m.FormattedMessage.Length - 1]);
                }
            }
        }

        [Fact]
        public async Task ServiceBusSessionSub_DifferentHosts_DifferentSessions()
        {
            using (var host1 = ServiceBusSessionsTestHelper.CreateHost<ServiceBusSessionsTestJobs1>(_nameResolver, true))
            using (var host2 = ServiceBusSessionsTestHelper.CreateHost<ServiceBusSessionsTestJobs2>(_nameResolver, true))
            {
                await host1.StartAsync();
                await host2.StartAsync();

                _waitHandle1 = new ManualResetEvent(initialState: false);
                _waitHandle2 = new ManualResetEvent(initialState: false);

                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message1", "test-session1");
                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message1", "test-session2");

                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message2", "test-session1");
                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message2", "test-session2");

                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message3", "test-session1");
                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message3", "test-session2");

                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message4", "test-session1");
                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message4", "test-session2");

                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message5", "test-session1");
                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message5", "test-session2");

                _waitHandle1.WaitOne(SBTimeout);
                _waitHandle2.WaitOne(SBTimeout);

                IEnumerable<LogMessage> logMessages1 = host1.GetTestLoggerProvider().GetAllLogMessages();
                List<LogMessage> consoleOutput1 = logMessages1.Where(m => m.Category == "Function.SBSub1Trigger.User").ToList();
                Assert.NotEmpty(logMessages1.Where(m => m.Category == "CustomMessagingProvider" && m.FormattedMessage.StartsWith("Custom processor Begin called!")));
                Assert.NotEmpty(logMessages1.Where(m => m.Category == "CustomMessagingProvider" && m.FormattedMessage.StartsWith("Custom processor End called!")));
                IEnumerable<LogMessage> logMessages2 = host2.GetTestLoggerProvider().GetAllLogMessages();
                List<LogMessage> consoleOutput2 = logMessages2.Where(m => m.Category == "Function.SBSub2Trigger.User").ToList();
                Assert.NotEmpty(logMessages2.Where(m => m.Category == "CustomMessagingProvider" && m.FormattedMessage.StartsWith("Custom processor Begin called!")));
                Assert.NotEmpty(logMessages2.Where(m => m.Category == "CustomMessagingProvider" && m.FormattedMessage.StartsWith("Custom processor End called!")));

                char sessionId1 = consoleOutput1[0].FormattedMessage[consoleOutput1[0].FormattedMessage.Length - 1];
                foreach (LogMessage m in consoleOutput1)
                {
                    Assert.Equal(sessionId1, m.FormattedMessage[m.FormattedMessage.Length - 1]);
                }

                char sessionId2 = consoleOutput2[0].FormattedMessage[consoleOutput1[0].FormattedMessage.Length - 1];
                foreach (LogMessage m in consoleOutput2)
                {
                    Assert.Equal(sessionId2, m.FormattedMessage[m.FormattedMessage.Length - 1]);
                }
            }
        }

        [Fact]
        public async Task ServiceBusSessionQueue_SessionLocks()
        {
            using (var host = ServiceBusSessionsTestHelper.CreateHost<ServiceBusSessionsTestJobs1>(_nameResolver, true))
            {
                await host.StartAsync();

                _waitHandle1 = new ManualResetEvent(initialState: false);
                _waitHandle2 = new ManualResetEvent(initialState: false);

                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message1", "test-session1");
                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message1", "test-session2");

                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message2", "test-session1");
                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message2", "test-session2");

                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message3", "test-session1");
                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message3", "test-session2");

                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message4", "test-session1");
                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message4", "test-session2");

                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message5", "test-session1");
                await ServiceBusSessionsTestHelper.WriteQueueMessage(_connectionString, _queueName, "message5", "test-session2");

                _waitHandle1.WaitOne(SBTimeout);
                _waitHandle2.WaitOne(SBTimeout);

                IEnumerable<LogMessage> logMessages1 = host.GetTestLoggerProvider().GetAllLogMessages();

                // filter out anything from the custom processor for easier validation.
                List<LogMessage> consoleOutput1 = logMessages1.Where(m => m.Category == "Function.SBQueue1Trigger.User").ToList();
                Assert.True(consoleOutput1.Count() == 10, ServiceBusSessionsTestHelper.GetLogsAsString(consoleOutput1));
                double seconsds = (consoleOutput1[5].Timestamp - consoleOutput1[4].Timestamp).TotalSeconds;
                Assert.True(seconsds > 90 && seconsds < 110, seconsds.ToString());
                for (int i=0; i < consoleOutput1.Count(); i++)
                {
                    if (i < 5)
                    {
                        Assert.Equal(consoleOutput1[i].FormattedMessage[consoleOutput1[0].FormattedMessage.Length - 1],
                            consoleOutput1[0].FormattedMessage[consoleOutput1[0].FormattedMessage.Length - 1]);
                    }
                    else
                    {
                        Assert.Equal(consoleOutput1[i].FormattedMessage[consoleOutput1[0].FormattedMessage.Length - 1],
                            consoleOutput1[5].FormattedMessage[consoleOutput1[0].FormattedMessage.Length - 1]);
                    }
                }
            }
        }

        [Fact]
        public async Task ServiceBusSessionSub_SessionLocks()
        {
            using (var host = ServiceBusSessionsTestHelper.CreateHost<ServiceBusSessionsTestJobs1>(_nameResolver, true))
            {
                await host.StartAsync();

                _waitHandle1 = new ManualResetEvent(initialState: false);
                _waitHandle2 = new ManualResetEvent(initialState: false);

                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message1", "test-session1");
                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message1", "test-session2");

                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message2", "test-session1");
                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message2", "test-session2");

                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message3", "test-session1");
                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message3", "test-session2");

                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message4", "test-session1");
                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message4", "test-session2");

                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message5", "test-session1");
                await ServiceBusSessionsTestHelper.WriteTopicMessage(_connectionString, _topicName, "message5", "test-session2");

                _waitHandle1.WaitOne(SBTimeout);
                _waitHandle2.WaitOne(SBTimeout);

                IEnumerable<LogMessage> logMessages1 = host.GetTestLoggerProvider().GetAllLogMessages();

                // filter out anything from the custom processor for easier validation.
                List<LogMessage> consoleOutput1 = logMessages1.Where(m => m.Category == "Function.SBSub1Trigger.User").ToList();
                Assert.True(consoleOutput1.Count() == 10, ServiceBusSessionsTestHelper.GetLogsAsString(consoleOutput1));
                double seconsds = (consoleOutput1[5].Timestamp - consoleOutput1[4].Timestamp).TotalSeconds;
                Assert.True(seconsds > 90 && seconsds < 110, seconsds.ToString());
                for (int i = 0; i < consoleOutput1.Count(); i++)
                {
                    if (i < 5)
                    {
                        Assert.Equal(consoleOutput1[i].FormattedMessage[consoleOutput1[0].FormattedMessage.Length - 1],
                            consoleOutput1[0].FormattedMessage[consoleOutput1[0].FormattedMessage.Length - 1]);
                    }
                    else
                    {
                        Assert.Equal(consoleOutput1[i].FormattedMessage[consoleOutput1[0].FormattedMessage.Length - 1],
                            consoleOutput1[5].FormattedMessage[consoleOutput1[0].FormattedMessage.Length - 1]);
                    }
                }
            }
        }

        private async Task Cleanup()
        {
            List<Task> tasks = new List<Task>();

            tasks.Add(ServiceBusSessionsTestHelper.CleanUpQueue(_connectionString, _queueName));
            tasks.Add(ServiceBusSessionsTestHelper.CleanUpSubscription(_connectionString, _topicName, _subscriptionName));

            await Task.WhenAll(tasks);
        }

        public class ServiceBusSessionsTestJobs1
        {
            public static void SBQueue1Trigger(
                [ServiceBusTrigger(_queueName, IsSessionsEnabled = true)] Message message, int deliveryCount,
                IMessageSession messageSession,
                ILogger log,
                string lockToken)
            {
                Assert.Equal(_queueName, messageSession.Path);
                Assert.Equal(1, deliveryCount);

                ServiceBusSessionsTestHelper.ProcessMessage(message, log, _waitHandle1, _waitHandle2);
            }

            public static void SBSub1Trigger(
                [ServiceBusTrigger(_topicName, _subscriptionName, IsSessionsEnabled = true)] Message message, int deliveryCount,
                IMessageSession messageSession,
                ILogger log,
                string lockToken)
            {
                Assert.Equal(EntityNameHelper.FormatSubscriptionPath(_topicName, _subscriptionName), messageSession.Path);
                Assert.Equal(1, deliveryCount);

                ServiceBusSessionsTestHelper.ProcessMessage(message, log, _waitHandle1, _waitHandle2);
            }
        }

        public class ServiceBusSessionsTestJobs2
        {
            public static void SBQueue2Trigger(
                [ServiceBusTrigger(_queueName, IsSessionsEnabled = true)] Message message,
                ILogger log)
            {

                ServiceBusSessionsTestHelper.ProcessMessage(message, log, _waitHandle1, _waitHandle2);
            }

            public static void SBSub2Trigger(
                [ServiceBusTrigger(_topicName, _subscriptionName, IsSessionsEnabled = true)] Message message,
                ILogger log)
            {

                ServiceBusSessionsTestHelper.ProcessMessage(message, log, _waitHandle1, _waitHandle2);
            }
        }


        public class CustomMessagingProvider : MessagingProvider
        {
            public const string CustomMessagingCategory = "CustomMessagingProvider";
            private readonly ILogger _logger;
            private readonly ServiceBusOptions _options;

            public CustomMessagingProvider(IOptions<ServiceBusOptions> serviceBusOptions, ILoggerFactory loggerFactory)
                : base(serviceBusOptions)
            {
                _options = serviceBusOptions.Value;
                _options.SessionHandlerOptions.MessageWaitTimeout = TimeSpan.FromSeconds(90);
                _options.SessionHandlerOptions.MaxConcurrentSessions = 1;
                _logger = loggerFactory?.CreateLogger(CustomMessagingCategory);
            }

            public override SessionMessageProcessor CreateSessionMessageProcessor(string entityPath, string connectionString)
            {
                if (entityPath == _queueName)
                {
                    return new CustomSessionMessageProcessor(new QueueClient(connectionString, entityPath), _options.SessionHandlerOptions, _logger);
                }
                else
                {
                    string[] arr = entityPath.Split('/');
                    return new CustomSessionMessageProcessor(new SubscriptionClient(connectionString, arr[0], arr[2]), _options.SessionHandlerOptions, _logger);
                }
            }

            private class CustomSessionMessageProcessor : SessionMessageProcessor
            {
                private readonly ILogger _logger;

                public CustomSessionMessageProcessor(ClientEntity clientEntity, SessionHandlerOptions messageOptions, ILogger logger)
                    : base(clientEntity, messageOptions)
                {
                    _logger = logger;
                }

                public override async Task<bool> BeginProcessingMessageAsync(IMessageSession session, Message message, CancellationToken cancellationToken)
                {
                    _logger?.LogInformation("Custom processor Begin called!" + ServiceBusSessionsTestHelper.GetStringMessage(message));
                    return await base.BeginProcessingMessageAsync(session, message, cancellationToken);
                }

                public override async Task CompleteProcessingMessageAsync(IMessageSession session, Message message, Executors.FunctionResult result, CancellationToken cancellationToken)
                {
                    _logger?.LogInformation("Custom processor End called!" + ServiceBusSessionsTestHelper.GetStringMessage(message));
                    await base.CompleteProcessingMessageAsync(session, message, result, cancellationToken);
                }
            }

            private Task ExceptionReceivedHandler(ExceptionReceivedEventArgs eventArgs)
            {
                return Task.CompletedTask;
            }
        }

        public void Dispose()
        {
            if (_waitHandle1 != null)
            {
                _waitHandle1.Dispose();
            }
            if (_waitHandle2 != null)
            {
                _waitHandle2.Dispose();
            }
        }
    }

    internal class ServiceBusSessionsTestHelper
    {
        private static SessionHandlerOptions sessionHandlerOptions = new SessionHandlerOptions(ExceptionReceivedHandler);

        public static async Task WriteQueueMessage(string connectionString, string queueName, string message, string sessionId = null)
        {
            QueueClient queueClient = new QueueClient(connectionString, queueName);
            Message messageObj = new Message(Encoding.UTF8.GetBytes(message));
            if (!string.IsNullOrEmpty(sessionId))
            {
                messageObj.SessionId = sessionId;
            }
            await queueClient.SendAsync(messageObj);
            await queueClient.CloseAsync();
        }

        public static async Task WriteTopicMessage(string connectionString, string topicName, string message, string sessionId = null)
        {
            TopicClient client = new TopicClient(connectionString, topicName);
            Message messageObj = new Message(Encoding.UTF8.GetBytes(message));
            if (!string.IsNullOrEmpty(sessionId))
            {
                messageObj.SessionId = sessionId;
            }
            await client.SendAsync(messageObj);
            await client.CloseAsync();
        }

        public static async Task CleanUpQueue(string connectionString, string queueName)
        {
            await ClenaUpEntity(connectionString, queueName);
        }

        public static async Task CleanUpSubscription(string connectionString, string topicName, string subscriptionName)
        {
            await ClenaUpEntity(connectionString, EntityNameHelper.FormatSubscriptionPath(topicName, subscriptionName));
        }

        private static async Task ClenaUpEntity(string connectionString, string entityPAth)
        {
            var client = new SessionClient(connectionString, entityPAth, ReceiveMode.PeekLock);
            client.OperationTimeout = TimeSpan.FromSeconds(5);

            IMessageSession session = null;
            try
            {
                session = await client.AcceptMessageSessionAsync();
                while (true)
                {
                    var message = await session.ReceiveAsync(TimeSpan.FromSeconds(5));
                    if (message != null)
                    {
                        await session.CompleteAsync(message.SystemProperties.LockToken);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (ServiceBusException)
            {
            }
            finally
            {
                if (session != null)
                {
                    await session.CloseAsync();
                }
            }
        }

        private static async Task ProcessMessagesInSessionAsync(IMessageSession messageSession, Message message, CancellationToken token)
        {
            await messageSession.CompleteAsync(message.SystemProperties.LockToken);
        }

        public static string GetStringMessage(Message message)
        {
            using (Stream stream = new MemoryStream(message.Body))
            using (TextReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        public static IHost CreateHost<T>(INameResolver reolver, bool addCustomProvider = false)
        {
            return new HostBuilder()
                .ConfigureDefaultTestHost<T>(b =>
                {
                    b.AddAzureStorage()
                    .AddServiceBus();
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton(reolver);
                    if (addCustomProvider)
                    {
                        services.AddSingleton<MessagingProvider, ServiceBusSessionsBusEndToEndTests.CustomMessagingProvider>();
                    }
                })
                .Build();
        }

        public static void ProcessMessage(Message message, ILogger log, EventWaitHandle waitHandle1, EventWaitHandle waitHandle2)
        {
            string messageString = ServiceBusSessionsTestHelper.GetStringMessage(message);
            log.LogInformation($"{messageString}-{message.SessionId}");

            if (messageString == "message5" && message.SessionId == "test-session1")
            {
                waitHandle1.Set();
            }

            if (messageString == "message5" && message.SessionId == "test-session2")
            {
                waitHandle2.Set();
            }
        }

        public static string GetLogsAsString(List<LogMessage> messages)
        {
            if (messages.Count() != 5 && messages.Count() != 10)
            {
            }

            string reuslt = string.Empty;
            foreach (LogMessage message in messages)
            {
                reuslt += message.FormattedMessage + System.Environment.NewLine;
            }
            return reuslt;
        }

        private static Task ExceptionReceivedHandler(ExceptionReceivedEventArgs args)
        {
            return Task.CompletedTask;
        }
    }
}