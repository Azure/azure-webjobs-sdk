// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
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
        public void ConfigureOptions_AppliesValuesCorrectly()
        {
            EventHubOptions options = CreateOptions();

            Assert.Equal(123, options.EventProcessorOptions.MaxBatchSize);
            Assert.Equal(TimeSpan.FromSeconds(33), options.EventProcessorOptions.ReceiveTimeout);
            Assert.Equal(true, options.EventProcessorOptions.EnableReceiverRuntimeMetric);
            Assert.Equal(123, options.EventProcessorOptions.PrefetchCount);
            Assert.Equal(true, options.EventProcessorOptions.InvokeProcessorAfterReceiveTimeout);
            Assert.Equal(5, options.BatchCheckpointFrequency);
            Assert.Equal(31, options.PartitionManagerOptions.LeaseDuration.TotalSeconds);
            Assert.Equal(21, options.PartitionManagerOptions.RenewInterval.TotalSeconds);
        }

        [Fact]
        public void ConfigureOptions_Format_Returns_Expected()
        {
            EventHubOptions options = CreateOptions();

            string format = options.Format();
            JObject iObj = JObject.Parse(format);
            EventHubOptions result = iObj.ToObject<EventHubOptions>();

            Assert.Equal(result.BatchCheckpointFrequency, 5);
            Assert.Equal(result.EventProcessorOptions.EnableReceiverRuntimeMetric, true);
            Assert.Equal(result.EventProcessorOptions.InvokeProcessorAfterReceiveTimeout, true);
            Assert.Equal(result.EventProcessorOptions.MaxBatchSize, 123);
            Assert.Equal(result.EventProcessorOptions.PrefetchCount, 123);
            Assert.Equal(result.EventProcessorOptions.ReceiveTimeout, TimeSpan.FromSeconds(33));
            Assert.Equal(result.PartitionManagerOptions.LeaseDuration, TimeSpan.FromSeconds(31));
            Assert.Equal(result.PartitionManagerOptions.RenewInterval, TimeSpan.FromSeconds(21));
        }

        private EventHubOptions CreateOptions()
        {
            string extensionPath = "AzureWebJobs:Extensions:EventHubs";
            var values = new Dictionary<string, string>
            {
                { $"{extensionPath}:EventProcessorOptions:MaxBatchSize", "123" },
                { $"{extensionPath}:EventProcessorOptions:ReceiveTimeout", "00:00:33" },
                { $"{extensionPath}:EventProcessorOptions:EnableReceiverRuntimeMetric", "true" },
                { $"{extensionPath}:EventProcessorOptions:PrefetchCount", "123" },
                { $"{extensionPath}:EventProcessorOptions:InvokeProcessorAfterReceiveTimeout", "true" },
                { $"{extensionPath}:BatchCheckpointFrequency", "5" },
                { $"{extensionPath}:PartitionManagerOptions:LeaseDuration", "00:00:31" },
                { $"{extensionPath}:PartitionManagerOptions:RenewInterval", "00:00:21" }
            };

            return TestHelpers.GetConfiguredOptions<EventHubOptions>(b =>
            {
                b.AddEventHubs();
            }, values);
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
            var options = host.Services.GetService<IOptions<EventHubOptions>>().Value;
            var eventProcessorOptions = options.EventProcessorOptions;
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