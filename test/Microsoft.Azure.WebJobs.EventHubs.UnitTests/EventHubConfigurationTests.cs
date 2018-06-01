// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Azure.WebJobs.ServiceBus;
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
            var filter = new LogCategoryFilter();
            filter.DefaultLevel = LogLevel.Debug;
            _loggerProvider = new TestLoggerProvider(filter.Filter);
            _loggerFactory.AddProvider(_loggerProvider);
        }

        [Fact]
        public void Initialize_PerformsExpectedRegistrations()
        {
            JobHostConfiguration config = new JobHostConfiguration();
            config.AddService<INameResolver>(new RandomNameResolver());

            TestLoggerProvider loggerProvider = new TestLoggerProvider();
            ILoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(loggerProvider);
            config.LoggerFactory = loggerFactory;

            EventHubConfiguration eventHubConfiguration = new EventHubConfiguration();

            IExtensionRegistry extensions = config.GetService<IExtensionRegistry>();
            ITriggerBindingProvider[] triggerBindingProviders = extensions.GetExtensions<ITriggerBindingProvider>().ToArray();
            Assert.Empty(triggerBindingProviders);
            IBindingProvider[] bindingProviders = extensions.GetExtensions<IBindingProvider>().ToArray();
            Assert.Empty(bindingProviders);

            ExtensionConfigContext context = new ExtensionConfigContext
            {
                Config = config,
            };
            ((IExtensionConfigProvider)eventHubConfiguration).Initialize(context);

            // ensure the EventHubTriggerAttributeBindingProvider was registered
            triggerBindingProviders = extensions.GetExtensions<ITriggerBindingProvider>().ToArray();
            EventHubTriggerAttributeBindingProvider triggerBindingProvider = (EventHubTriggerAttributeBindingProvider)triggerBindingProviders.Single();
            Assert.NotNull(triggerBindingProvider);

            // ensure the EventProcessorOptions ExceptionReceived event is wired up
            var eventProcessorOptions = eventHubConfiguration.EventProcessorOptions;
            var ex = new EventHubsException(false, "Kaboom!");
            var ctor = typeof(ExceptionReceivedEventArgs).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
            var args = (ExceptionReceivedEventArgs)ctor.Invoke(new object[] { "TestHostName", "TestPartitionId", ex, "TestAction" });
            var handler = (Action<ExceptionReceivedEventArgs>)eventProcessorOptions.GetType().GetField("exceptionHandler", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(eventProcessorOptions);
            handler.Method.Invoke(handler.Target, new object[] { args });

            string expectedMessage = "EventProcessorHost error (Action=TestAction, HostName=TestHostName, PartitionId=TestPartitionId)";
            var logMessage = loggerProvider.GetAllLogMessages().Single();
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
            EventHubConfiguration.LogExceptionReceivedEvent(e, _loggerFactory);

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
            EventHubConfiguration.LogExceptionReceivedEvent(e, _loggerFactory);

            string expectedMessage = "EventProcessorHost error (Action=TestAction, HostName=TestHostName, PartitionId=TestPartitionId)";
            var logMessage = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Debug, logMessage.Level);
            Assert.Same(ex, logMessage.Exception);
            Assert.Equal(expectedMessage, logMessage.FormattedMessage);
        }

        [Fact]
        public void LogExceptionReceivedEvent_NonMessagingException_LoggedAsError()
        {
            var ex = new MissingMethodException("What method??");
            var ctor = typeof(ExceptionReceivedEventArgs).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
            var e = (ExceptionReceivedEventArgs)ctor.Invoke(new object[] { "TestHostName", "TestPartitionId", ex, "TestAction" });
            EventHubConfiguration.LogExceptionReceivedEvent(e, _loggerFactory);

            string expectedMessage = "EventProcessorHost error (Action=TestAction, HostName=TestHostName, PartitionId=TestPartitionId)";
            var logMessage = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Error, logMessage.Level);
            Assert.Same(ex, logMessage.Exception);
            Assert.Equal(expectedMessage, logMessage.FormattedMessage);
        }
    }
}