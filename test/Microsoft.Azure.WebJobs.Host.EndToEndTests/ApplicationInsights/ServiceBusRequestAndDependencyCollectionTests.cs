// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests.ApplicationInsights
{
    public class ServiceBusRequestAndDependencyCollectionTests : IDisposable
    {
        private const string _queueName = "core-test-queue1";
        private const string _mockApplicationInsightsKey = "some_key";
        private readonly Uri _endpoint;
        private readonly string _connectionString;
        private readonly TestTelemetryChannel _channel = new TestTelemetryChannel();
        private readonly ServiceBusClient _client;
        private static readonly AutoResetEvent _functionWaitHandle = new AutoResetEvent(false);

        public ServiceBusRequestAndDependencyCollectionTests()
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddTestSettings()
                .Build();

            _connectionString = config.GetConnectionStringOrSetting("AzureWebJobsServiceBus");
            _endpoint = ServiceBusConnectionStringProperties.Parse(_connectionString).Endpoint;
            _client = new ServiceBusClient(_connectionString);
        }

        [Theory]
        [InlineData("message", true)]
        [InlineData("throw", false)]
        public async Task ServiceBusDependenciesAndRequestAreTracked(string message, bool success)
        {
            using (var host = ConfigureHost())
            {
                await host.StartAsync();

                await host.GetJobHost()
                    .CallAsync(typeof(ServiceBusRequestAndDependencyCollectionTests).GetMethod(nameof(ServiceBusOut)), new { input = message });
                _functionWaitHandle.WaitOne();

                await Task.Delay(1000);

                await host.StopAsync();
            }

            List<RequestTelemetry> requests = _channel.Telemetries.OfType<RequestTelemetry>().ToList();
            List<DependencyTelemetry> dependencies = _channel.Telemetries.OfType<DependencyTelemetry>().ToList();
            List<TraceTelemetry> traces = _channel.Telemetries.OfType<TraceTelemetry>().ToList();

            Assert.Equal(2, requests.Count);

            // One dependency for the 'Send' from ServiceBusOut
            // One dependency for the 'Complete' call in ServiceBusTrigger
            // A final dependency from Azure ServiceBus SDK
            Assert.Equal(3, dependencies.Count);
            Assert.Single(dependencies, d => d.Name == "ServiceBusReceiver.Complete");
            Assert.Single(dependencies, d => d.Name == "ServiceBusSender.Send");

            var sbOutDependency = dependencies.Single(d => d.Name == "ServiceBusSender.Send");
            var messageDependency = dependencies.Single(d => d.Name == "Message");
            var completeDependency = dependencies.Single(d => d.Name == "ServiceBusReceiver.Complete");

            var sbTriggerRequest = requests.Single(r => r.Context.Operation.Name == nameof(ServiceBusTrigger));
            var manualCallRequest = requests.Single(r => r.Context.Operation.Name == nameof(ServiceBusOut));

            var operationId = manualCallRequest.Context.Operation.Id;
            Assert.Equal(operationId, sbTriggerRequest.Context.Operation.Id);

            ValidateServiceBusRequest(sbTriggerRequest, success, _endpoint, _queueName, nameof(ServiceBusTrigger), operationId, messageDependency.Id);
            ValidateServiceBusDependency(
                sbOutDependency,
                _endpoint,
                _queueName, 
                "ServiceBusSender.Send",
                nameof(ServiceBusOut),
                operationId, 
                manualCallRequest.Id,
                LogCategories.Bindings);

            ValidateServiceBusDependency(
                completeDependency,
                _endpoint,
                _queueName,
                "ServiceBusReceiver.Complete",
                nameof(ServiceBusTrigger),
                operationId,
                sbTriggerRequest.Id,
                LogCategories.CreateFunctionCategory(nameof(ServiceBusTrigger)));

            var allFunctionTraces = traces.Where(t => t.Context.Operation.Id == sbTriggerRequest.Context.Operation.Id).ToList();
            var manualFunctionTraces = traces.Where(t => t.Context.Operation.Id == sbTriggerRequest.Context.Operation.Id && 
                                                         t.Context.Operation.ParentId == manualCallRequest.Id).ToList();

            var triggerFunctionTraces = traces.Where(t => t.Context.Operation.Id == sbTriggerRequest.Context.Operation.Id &&
                                                          t.Context.Operation.ParentId == sbTriggerRequest.Id).ToList();
            Assert.Equal(success ? 15 : 16, allFunctionTraces.Count);

            // manual function writes 'executing' and 'executed', and one more from SB SDK.
            Assert.Equal(3, manualFunctionTraces.Count);

            // trigger writes 'executing', 'executed', trigger and log inside function + 2 errors on exception
            Assert.Equal(success ? 8 : 9, triggerFunctionTraces.Count);
        }

        [Fact]
        public async Task ServiceBusRequestMultiHost()
        {
            var sender = _client.CreateSender(_queueName);
            var message = new ServiceBusMessage("message")
            {
                ContentType = "text/plain",
            };

            await sender.SendMessageAsync(message);
            using (var host1 = ConfigureHost())
            using (var host2 = ConfigureHost())
            {
                await host1.StartAsync();
                await host2.StartAsync();

                _functionWaitHandle.WaitOne();

                await Task.Delay(1000);

                await host1.StopAsync();
                await host2.StopAsync();
            }

            List<RequestTelemetry> requests = _channel.Telemetries.OfType<RequestTelemetry>().ToList();
            List<DependencyTelemetry> dependencies = _channel.Telemetries.OfType<DependencyTelemetry>().ToList();
            List<TraceTelemetry> traces = _channel.Telemetries.OfType<TraceTelemetry>().Where(t => t.Context.Operation.Id != null).ToList();

            Assert.Single(requests);
            Assert.Single(dependencies);
            Assert.Equal(16, traces.Count);
        }

        [Fact]
        public async Task ServiceBusRequestWithoutParent()
        {
            var sender = _client.CreateSender(_queueName);
            var message = new ServiceBusMessage("message")
            {
                ContentType = "text/plain",
            };

            await sender.SendMessageAsync(message);
            using (var host = ConfigureHost())
            {
                await host.StartAsync();

                _functionWaitHandle.WaitOne();
                await Task.Delay(1000);

                await host.StopAsync();
            }

            List<RequestTelemetry> requests = _channel.Telemetries.OfType<RequestTelemetry>().ToList();
            List<DependencyTelemetry> dependencies = _channel.Telemetries.OfType<DependencyTelemetry>().ToList();
            List<TraceTelemetry> traces = _channel.Telemetries.OfType<TraceTelemetry>().ToList();

            Assert.Single(requests);

            // The call to Complete the message registers as a dependency
            Assert.Single(dependencies);

            Assert.Single(dependencies, d => d.Name == "ServiceBusReceiver.Complete");
            var completeDependency = dependencies.Single(d => d.Name == "ServiceBusReceiver.Complete");

            var request = requests.Single();

            Assert.NotNull(request.Context.Operation.Id);

            ValidateServiceBusRequest(request, true, _endpoint, _queueName, nameof(ServiceBusTrigger), null, null);
            ValidateServiceBusDependency(
                completeDependency,
                _endpoint, 
                _queueName, 
                "ServiceBusReceiver.Complete",
                nameof(ServiceBusTrigger), 
                request.Context.Operation.Id,
                request.Id,
                LogCategories.CreateFunctionCategory(nameof(ServiceBusTrigger)));

            // Make sure that the trigger traces are correlated
            traces = _channel.Telemetries.OfType<TraceTelemetry>().Where(t => t.Context.Operation.Id == request.Context.Operation.Id).ToList();
            Assert.Equal(9, traces.Count);

            foreach (var trace in traces)
            {
                Assert.Equal(request.Context.Operation.Id, trace.Context.Operation.Id);

                if (trace.Properties.TryGetValue("EventName", out string value) && value == "CompleteMessageCompleteCore")
                {
                    // This message is a child
                    continue;
                }

                Assert.Equal(request.Id, trace.Context.Operation.ParentId);
            }
        }

        [Fact(Skip = "Azure SDK ID handling has changed")]
        public async Task ServiceBusRequestLegacyCompatibleParent()
        {
            var sender = _client.CreateSender(_queueName);

            var compatibleRoot = ActivityTraceId.CreateRandom().ToHexString();
            var legacyParent = $"|{compatibleRoot}.1.2.3.";
            var message = new ServiceBusMessage("message")
            {
                ContentType = "text/plain",
                ApplicationProperties = { ["Diagnostic-Id"] = legacyParent }
            };

            await sender.SendMessageAsync(message);

            using (var host = ConfigureHost())
            {
                await host.StartAsync();

                _functionWaitHandle.WaitOne();
                await Task.Delay(1000);

                await host.StopAsync();
            }

            List<RequestTelemetry> requests = _channel.Telemetries.OfType<RequestTelemetry>().ToList();
            List<DependencyTelemetry> dependencies = _channel.Telemetries.OfType<DependencyTelemetry>().ToList();
            List<TraceTelemetry> traces = _channel.Telemetries.OfType<TraceTelemetry>().ToList();

            Assert.Single(requests);

            // The call to Complete the message registers as a dependency
            Assert.Single(dependencies);

            Assert.Single(dependencies, d => d.Name == "ServiceBusReceiver.Complete");
            var completeDependency = dependencies.Single(d => d.Name == "ServiceBusReceiver.Complete");

            var request = requests.Single();

            Assert.False(request.Properties.TryGetValue("ai_legacyRootId", out var legacyRoot));

            ValidateServiceBusRequest(request, true, _endpoint, _queueName, nameof(ServiceBusTrigger), compatibleRoot, legacyParent);
            ValidateServiceBusDependency(
                completeDependency,
                _endpoint,
                _queueName,
                "ServiceBusReceiver.Complete",
                nameof(ServiceBusTrigger),
                request.Context.Operation.Id,
                request.Id,
                LogCategories.CreateFunctionCategory(nameof(ServiceBusTrigger)));

            // Make sure that the trigger traces are correlated
            traces = _channel.Telemetries.OfType<TraceTelemetry>().Where(t => t.Context.Operation.Id == request.Context.Operation.Id).ToList();
            Assert.Equal(4, traces.Count());

            foreach (var trace in traces)
            {
                Assert.Equal(request.Context.Operation.Id, trace.Context.Operation.Id);
                Assert.Equal(request.Id, trace.Context.Operation.ParentId);
            }
        }

        [Fact(Skip = "Azure SDK ID handling has changed")]
        public async Task ServiceBusRequestLegacyNotCompatibleParent()
        {
            var sender = _client.CreateSender(_queueName);

            var legacyParent = "|legacyId.1.2.3.";
            var message = new ServiceBusMessage("message")
            {
                ContentType = "text/plain",
                ApplicationProperties = { ["Diagnostic-Id"] = legacyParent }
            };

            await sender.SendMessageAsync(message);

            using (var host = ConfigureHost())
            {
                await host.StartAsync();

                _functionWaitHandle.WaitOne();
                await Task.Delay(1000);

                await host.StopAsync();
            }

            List<RequestTelemetry> requests = _channel.Telemetries.OfType<RequestTelemetry>().ToList();
            List<DependencyTelemetry> dependencies = _channel.Telemetries.OfType<DependencyTelemetry>().ToList();
            List<TraceTelemetry> traces = _channel.Telemetries.OfType<TraceTelemetry>().ToList();

            Assert.Single(requests);

            // The call to Complete the message registers as a dependency
            Assert.Single(dependencies);

            Assert.Single(dependencies, d => d.Name == "ServiceBusReceiver.Complete");
            var completeDependency = dependencies.Single(d => d.Name == "ServiceBusReceiver.Complete");

            var request = requests.Single();

            Assert.NotNull(request.Context.Operation.Id);

            Assert.True(request.Properties.TryGetValue("ai_legacyRootId", out var legacyRoot));
            Assert.Equal("legacyId", legacyRoot);
            ValidateServiceBusRequest(request, true, _endpoint, _queueName, nameof(ServiceBusTrigger), request.Context.Operation.Id, legacyParent);
            ValidateServiceBusDependency(
                completeDependency,
                _endpoint,
                _queueName,
                "ServiceBusReceiver.Complete",
                nameof(ServiceBusTrigger),
                request.Context.Operation.Id,
                request.Id,
                LogCategories.CreateFunctionCategory(nameof(ServiceBusTrigger)));

            // Make sure that the trigger traces are correlated
            traces = _channel.Telemetries.OfType<TraceTelemetry>().Where(t => t.Context.Operation.Id == request.Context.Operation.Id).ToList();
            Assert.Equal(4, traces.Count());

            foreach (var trace in traces)
            {
                Assert.Equal(request.Context.Operation.Id, trace.Context.Operation.Id);
                Assert.Equal(request.Id, trace.Context.Operation.ParentId);
            }
        }

        [NoAutomaticTrigger]
        public static void ServiceBusOut(
            string input,
            [ServiceBus(_queueName)] out string message,
            TextWriter logger)
        {
            message = input;
        }

        public static async Task ServiceBusTrigger(
            [ServiceBusTrigger(_queueName)] ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageReceiver,
            TextWriter logger)
        {
            try
            {
                logger.WriteLine($"C# script processed queue message: '{message}'");

                if (message.Body.ToString() == "throw")
                {
                    throw new InvalidOperationException("boom!");
                }
            }
            finally
            {
                await messageReceiver.CompleteMessageAsync(message);
                _functionWaitHandle.Set();
            }
        }

        private void ValidateServiceBusRequest(
            RequestTelemetry request,
            bool success,
            Uri endpoint,
            string queueName,
            string operationName,
            string operationId,
            string parentId)
        {
            Assert.Equal($"{endpoint.Host}/{queueName}", request.Source);
            Assert.Null(request.Url);

            Assert.True(request.Properties.ContainsKey(LogConstants.FunctionExecutionTimeKey));
            Assert.True(double.TryParse(request.Properties[LogConstants.FunctionExecutionTimeKey], out double functionDuration));
            Assert.True(request.Duration.TotalMilliseconds >= functionDuration);

            TelemetryValidationHelpers.ValidateRequest(request, operationName, operationName, operationId, parentId, LogCategories.Results,
                success ? LogLevel.Information : LogLevel.Error, success);
        }

        private void ValidateServiceBusDependency(
            DependencyTelemetry dependency,
            Uri endpoint,
            string queueName,
            string name,
            string operationName,
            string operationId,
            string parentId,
            string category)
        {
            Assert.Equal($"{endpoint.Host}/{queueName}", dependency.Target);
            Assert.Equal("Azure Service Bus", dependency.Type);
            Assert.Equal(name, dependency.Name);
            Assert.True(dependency.Success);
            Assert.True(string.IsNullOrEmpty(dependency.Data));
            TelemetryValidationHelpers.ValidateDependency(dependency, operationName, operationId, parentId, category);
        }

        public IHost ConfigureHost()
        {
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<ServiceBusRequestAndDependencyCollectionTests>(b =>
                {
                    b.AddServiceBus(o =>
                    {
                        // We'll complete these ourselves as we don't
                        // want failures constantly retrying.
                        o.AutoCompleteMessages = false;
                    });
                })
                .ConfigureLogging(b =>
                {
                    b.SetMinimumLevel(LogLevel.Information);
                    b.AddApplicationInsightsWebJobs(o => o.InstrumentationKey = _mockApplicationInsightsKey);
                })
                .Build();

            TelemetryConfiguration telemteryConfiguration = host.Services.GetService<TelemetryConfiguration>();
            telemteryConfiguration.TelemetryChannel = _channel;

            return host;
        }

        public void Dispose()
        {
            _channel?.Dispose();
            CleanUpEntity().GetAwaiter().GetResult();
        }

        private async Task<int> CleanUpEntity()
        {
            var messageReceiver = _client.CreateReceiver(
                _queueName, new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });
            int count = 0;
            do
            {
                var messages = await messageReceiver.ReceiveMessagesAsync(10, TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                if (messages != null && messages.Count > 0)
                {
                    count += messages.Count;
                }
                else
                {
                    break;
                }
            } while (true);
            await messageReceiver.CloseAsync();
            return count;
        }
    }
}