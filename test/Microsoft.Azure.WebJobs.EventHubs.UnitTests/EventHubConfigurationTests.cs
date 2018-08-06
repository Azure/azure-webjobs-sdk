// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.WebJobs.EventHubs.UnitTests
{
    public class EventHubConfigurationTests
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly TestLoggerProvider _loggerProvider;

        public EventHubConfigurationTests()
        {
            _loggerFactory = new LoggerFactory();

            _loggerProvider = new TestLoggerProvider();
            _loggerFactory.AddProvider(_loggerProvider);
        }

        [Fact]
        public void Initialize_PerformsExpectedRegistrations()
        {
            var host = new HostBuilder()
                .ConfigureDefaultTestHost(builder =>
                {
                    builder.AddEventHubs();
                })
                .ConfigureServices(c =>
                {
                    c.AddSingleton<INameResolver>(new RandomNameResolver());
                })
                .Build();

            IExtensionRegistry extensions = host.Services.GetService<IExtensionRegistry>();

            // ensure the EventHubTriggerAttributeBindingProvider was registered
            var triggerBindingProviders = extensions.GetExtensions<ITriggerBindingProvider>().ToArray();
            EventHubTriggerAttributeBindingProvider triggerBindingProvider = triggerBindingProviders.OfType<EventHubTriggerAttributeBindingProvider>().Single();
            Assert.NotNull(triggerBindingProvider);

            // ensure the EventProcessorOptions ExceptionReceived event is wired up
            var eventHubConfiguration = host.Services.GetService<EventHubConfiguration>();
            var eventProcessorOptions = eventHubConfiguration.GetOptions();
            var ex = new EventHubsException(false, "Kaboom!");
            var ctor = typeof(ExceptionReceivedEventArgs).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
            var args = (ExceptionReceivedEventArgs)ctor.Invoke(new object[] { "TestHostName", "TestPartitionId", ex, "TestAction" });
            var handler = (Action<ExceptionReceivedEventArgs>)eventProcessorOptions.GetType().GetField("exceptionHandler", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(eventProcessorOptions);
            handler.Method.Invoke(handler.Target, new object[] { args });

            string expectedMessage = "EventProcessorHost error (Action=TestAction, HostName=TestHostName, PartitionId=TestPartitionId)";
            var logMessage = host.GetTestLoggerProvider().GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Error, logMessage.Level);
            Assert.Equal(expectedMessage, logMessage.FormattedMessage);
            Assert.Same(ex, logMessage.Exception);
        }

        [Fact]
        public void LogExceptionReceivedEvent_NonTransientEvent_LoggedAsError()
        {
            var ex = new EventHubsException(false);
            Assert.False(ex.IsTransient);
            var ctor = typeof(ExceptionReceivedEventArgs).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
            var e = (ExceptionReceivedEventArgs)ctor.Invoke(new object[] { "TestHostName", "TestPartitionId", ex, "TestAction" });
            EventHubExtensionConfigProvider.LogExceptionReceivedEvent(e, _loggerFactory);

            string expectedMessage = "EventProcessorHost error (Action=TestAction, HostName=TestHostName, PartitionId=TestPartitionId)";
            var logMessage = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Error, logMessage.Level);
            Assert.Same(ex, logMessage.Exception);
            Assert.Equal(expectedMessage, logMessage.FormattedMessage);
        }

        [Fact]
        public void LogExceptionReceivedEvent_TransientEvent_LoggedAsVerbose()
        {
            var ex = new EventHubsException(true);
            Assert.True(ex.IsTransient);
            var ctor = typeof(ExceptionReceivedEventArgs).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
            var e = (ExceptionReceivedEventArgs)ctor.Invoke(new object[] { "TestHostName", "TestPartitionId", ex, "TestAction" });
            EventHubExtensionConfigProvider.LogExceptionReceivedEvent(e, _loggerFactory);

            string expectedMessage = "EventProcessorHost error (Action=TestAction, HostName=TestHostName, PartitionId=TestPartitionId)";
            var logMessage = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Information, logMessage.Level);
            Assert.Same(ex, logMessage.Exception);
            Assert.Equal(expectedMessage, logMessage.FormattedMessage);
        }

        [Fact]
        public void LogExceptionReceivedEvent_OperationCanceledException_LoggedAsVerbose()
        {
            var ex = new OperationCanceledException("Testing");
            var ctor = typeof(ExceptionReceivedEventArgs).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
            var e = (ExceptionReceivedEventArgs)ctor.Invoke(new object[] { "TestHostName", "TestPartitionId", ex, "TestAction" });
            EventHubExtensionConfigProvider.LogExceptionReceivedEvent(e, _loggerFactory);

            string expectedMessage = "EventProcessorHost error (Action=TestAction, HostName=TestHostName, PartitionId=TestPartitionId)";
            var logMessage = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Information, logMessage.Level);
            Assert.Same(ex, logMessage.Exception);
            Assert.Equal(expectedMessage, logMessage.FormattedMessage);
        }

        [Fact]
        public void LogExceptionReceivedEvent_NonMessagingException_LoggedAsError()
        {
            var ex = new MissingMethodException("What method??");
            var ctor = typeof(ExceptionReceivedEventArgs).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
            var e = (ExceptionReceivedEventArgs)ctor.Invoke(new object[] { "TestHostName", "TestPartitionId", ex, "TestAction" });
            EventHubExtensionConfigProvider.LogExceptionReceivedEvent(e, _loggerFactory);

            string expectedMessage = "EventProcessorHost error (Action=TestAction, HostName=TestHostName, PartitionId=TestPartitionId)";
            var logMessage = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Error, logMessage.Level);
            Assert.Same(ex, logMessage.Exception);
            Assert.Equal(expectedMessage, logMessage.FormattedMessage);
        }

        [Fact]
        public void LogExceptionReceivedEvent_PartitionExceptions_LoggedAsInfo()
        {
            var ctor = typeof(ReceiverDisconnectedException).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(string) }, null);
            var ex = (ReceiverDisconnectedException)ctor.Invoke(new object[] { "New receiver with higher epoch of '30402' is created hence current receiver with epoch '30402' is getting disconnected." });
            ctor = typeof(ExceptionReceivedEventArgs).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
            var e = (ExceptionReceivedEventArgs)ctor.Invoke(new object[] { "TestHostName", "TestPartitionId", ex, "TestAction" });
            EventHubExtensionConfigProvider.LogExceptionReceivedEvent(e, _loggerFactory);

            string expectedMessage = "EventProcessorHost error (Action=TestAction, HostName=TestHostName, PartitionId=TestPartitionId)";
            var logMessage = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Information, logMessage.Level);
            Assert.Same(ex, logMessage.Exception);
            Assert.Equal(expectedMessage, logMessage.FormattedMessage);
        }
    }
}