// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.ServiceBus.Bindings;
using Microsoft.Azure.WebJobs.ServiceBus.Config;
using Microsoft.Azure.WebJobs.ServiceBus.Triggers;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests.Config
{
    public class ServiceBusExtensionConfigTests
    {
        [Fact]
        public async Task Initialize_PerformsExpectedRegistrations()
        {
            JobHostConfiguration config = new JobHostConfiguration();
            config.AddService<INameResolver>(new RandomNameResolver());

            TestLoggerProvider loggerProvider = new TestLoggerProvider();
            ILoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(loggerProvider);
            config.LoggerFactory = loggerFactory;

            ServiceBusConfiguration serviceBusConfig = new ServiceBusConfiguration();
            ServiceBusExtensionConfig serviceBusExtensionConfig = new ServiceBusExtensionConfig(serviceBusConfig);

            IExtensionRegistry extensions = config.GetService<IExtensionRegistry>();
            ITriggerBindingProvider[] triggerBindingProviders = extensions.GetExtensions<ITriggerBindingProvider>().ToArray();
            Assert.Empty(triggerBindingProviders);
            IBindingProvider[] bindingProviders = extensions.GetExtensions<IBindingProvider>().ToArray();
            Assert.Empty(bindingProviders);

            ExtensionConfigContext context = new ExtensionConfigContext
            {
                Config = config,
            };

            serviceBusExtensionConfig.Initialize(context);

            // ensure the ServiceBusTriggerAttributeBindingProvider was registered
            triggerBindingProviders = extensions.GetExtensions<ITriggerBindingProvider>().ToArray();
            Assert.Single(triggerBindingProviders);
            ServiceBusTriggerAttributeBindingProvider triggerBindingProvider = (ServiceBusTriggerAttributeBindingProvider)triggerBindingProviders[0];
            Assert.NotNull(triggerBindingProvider);

            // ensure the ServiceBusAttributeBindingProvider was registered
            bindingProviders = extensions.GetExtensions<IBindingProvider>().ToArray();
            Assert.Single(bindingProviders);
            ServiceBusAttributeBindingProvider bindingProvider = (ServiceBusAttributeBindingProvider)bindingProviders[0];
            Assert.NotNull(bindingProvider);

            // ensure the default MessageOptions exception handler is wired up
            var messageOptions = serviceBusConfig.MessageOptions;
            var ex = new ServiceBusException(false);
            var args = new ExceptionReceivedEventArgs(ex, "TestAction", "TestEndpoint", "TestEntityPath", "TestClientId");
            Assert.NotNull(serviceBusConfig.ExceptionHandler);

            // invoke the handler and make sure it logs
            await serviceBusConfig.MessageOptions.ExceptionReceivedHandler(args);
            string expectedMessage = "MessageReceiver error (Action=TestAction, ClientId=TestClientId, EntityPath=TestEntityPath, Endpoint=TestEndpoint)";
            var logMessage = loggerProvider.GetAllLogMessages().Single();
            Assert.Equal(LogLevel.Error, logMessage.Level);
            Assert.Equal(expectedMessage, logMessage.FormattedMessage);
            Assert.Same(ex, logMessage.Exception);
        }
    }
}
