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
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.WebJobs.EventHubs.UnitTests
{
    public class EventHubConfigurationTests
    {
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
            var args = (ExceptionReceivedEventArgs)ctor.Invoke(new object[] { "TestHostName", "TestPartitionId", ex, "Testing" });
            var handler = (Action<ExceptionReceivedEventArgs>)eventProcessorOptions.GetType().GetField("exceptionHandler", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(eventProcessorOptions);
            handler.Method.Invoke(handler.Target, new object[] { args });

            string expectedMessage = "EventProcessorHost error (Action=Testing)";
            var logMessage = loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Error, logMessage.Level);
            Assert.Equal(expectedMessage, logMessage.FormattedMessage);
            Assert.Same(ex, logMessage.Exception);
        }
    }
}