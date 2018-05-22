// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceBus.Messaging;
using Xunit;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests.Config
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
            Assert.Equal(0, triggerBindingProviders.Length);
            IBindingProvider[] bindingProviders = extensions.GetExtensions<IBindingProvider>().ToArray();
            Assert.Equal(0, bindingProviders.Length);

            var traceWriter = new TestTraceWriter(TraceLevel.Verbose);
            ExtensionConfigContext context = new ExtensionConfigContext
            {
                Config = config,
                Trace = traceWriter
            };
            ((IExtensionConfigProvider)eventHubConfiguration).Initialize(context);

            // ensure the EventHubTriggerAttributeBindingProvider was registered
            triggerBindingProviders = extensions.GetExtensions<ITriggerBindingProvider>().ToArray();
            Assert.Equal(1, triggerBindingProviders.Length);
            EventHubTriggerAttributeBindingProvider triggerBindingProvider = (EventHubTriggerAttributeBindingProvider)triggerBindingProviders[0];
            Assert.NotNull(triggerBindingProvider);

            // ensure the EventProcessorOptions ExceptionReceived event is wired up
            var eventProcessorOptions = eventHubConfiguration.GetOptions();
            var ex = new Exception("Kaboom!");
            var args = new ExceptionReceivedEventArgs(ex, "Testing");
            var eventDelegate = (MulticastDelegate)eventProcessorOptions.GetType().GetField("ExceptionReceived", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(eventProcessorOptions);
            Assert.Equal(1, eventDelegate.GetInvocationList().Length);
            var handler = eventDelegate.GetInvocationList().Single();
            handler.Method.Invoke(handler.Target, new object[] { null, args });

            string expectedMessage = "EventProcessorHost error (Action=Testing)";
            var trace = traceWriter.Traces.Last();
            Assert.Equal(expectedMessage, trace.Message);
            Assert.Same(ex, trace.Exception);

            var logMessage = loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Error, logMessage.Level);
            Assert.Equal(expectedMessage, logMessage.FormattedMessage);
            Assert.Same(ex, logMessage.Exception);
        }
    }
}
