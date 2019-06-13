// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
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
        private readonly string _endpoint;
        private readonly string _connectionString;
        private readonly TestTelemetryChannel _channel = new TestTelemetryChannel();
        private static readonly AutoResetEvent _functionWaitHandle = new AutoResetEvent(false);

        public ServiceBusRequestAndDependencyCollectionTests()
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddTestSettings()
                .Build();

            _connectionString = config.GetConnectionStringOrSetting(ServiceBus.Constants.DefaultConnectionStringName);

            var connStringBuilder = new ServiceBusConnectionStringBuilder(_connectionString);
            _endpoint = connStringBuilder.Endpoint;
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
            Assert.Equal(2, dependencies.Count);
            Assert.Single(dependencies, d => d.Name == "Complete");
            Assert.Single(dependencies, d => d.Name == "Send");

            var sbOutDependency = dependencies.Single(d => d.Name == "Send");
            var completeDependency = dependencies.Single(d => d.Name == "Complete");

            var sbTriggerRequest = requests.Single(r => r.Context.Operation.Name == nameof(ServiceBusTrigger));
            var manualCallRequest = requests.Single(r => r.Context.Operation.Name == nameof(ServiceBusOut));

            string manualOperationId = manualCallRequest.Context.Operation.Id;
            string triggerOperationId = sbTriggerRequest.Context.Operation.Id;

            // currently ApplicationInsights supports 2 parallel correlation schemes:
            // legacy and W3C, they both appear in telemetry. UX handles all differences in operation Ids. 
            // This will be resolved in next .NET SDK on Activity level
            string dependencyLegacyId = sbOutDependency.Properties.Single(p => p.Key == "ai_legacyRequestId").Value;
            string triggerCallLegacyRootId = sbTriggerRequest.Properties.Single(p => p.Key == "ai_legacyRootId").Value;
            string manualCallLegacyRootId = manualCallRequest.Properties.Single(p => p.Key == "ai_legacyRootId").Value;

            Assert.Equal(sbTriggerRequest.Context.Operation.ParentId, dependencyLegacyId);
            Assert.Equal(manualOperationId, sbOutDependency.Context.Operation.Id);
            Assert.Equal(manualCallLegacyRootId, triggerCallLegacyRootId);

            ValidateServiceBusRequest(sbTriggerRequest, success, _endpoint, _queueName, nameof(ServiceBusTrigger), triggerOperationId, dependencyLegacyId);
            ValidateServiceBusDependency(
                sbOutDependency,
                _endpoint,
                _queueName, 
                "Send", 
                nameof(ServiceBusOut), 
                manualOperationId, 
                manualCallRequest.Id,
                LogCategories.Bindings);

            ValidateServiceBusDependency(
                completeDependency,
                _endpoint,
                _queueName,
                "Complete",
                nameof(ServiceBusTrigger),
                sbTriggerRequest.Context.Operation.Id,
                sbTriggerRequest.Id,
                LogCategories.CreateFunctionCategory(nameof(ServiceBusTrigger)));

            var functionTraces = traces.Where(t => t.Context.Operation.Id == sbTriggerRequest.Context.Operation.Id).ToList();
            Assert.Equal(success ? 4 : 6, functionTraces.Count);

            foreach (var trace in functionTraces)
            {
                Assert.Equal(sbTriggerRequest.Context.Operation.Id, trace.Context.Operation.Id);
                Assert.Equal(sbTriggerRequest.Id, trace.Context.Operation.ParentId);
            }
        }

        [Fact]
        public async Task ServiceBusRequestMultiHost()
        {
            var sender = new MessageSender(_connectionString, _queueName);
            await sender.SendAsync(new Message { Body = Encoding.UTF8.GetBytes("message"), ContentType = "text/plain" });

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
            Assert.Equal(4, traces.Count);
        }

        [Fact]
        public async Task ServiceBusRequestWithoutParent()
        {
            var sender = new MessageSender(_connectionString, _queueName);
            await sender.SendAsync(new Message { Body = Encoding.UTF8.GetBytes("message"), ContentType = "text/plain" });

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

            Assert.Single(dependencies, d => d.Name == "Complete");
            var completeDependency = dependencies.Single(d => d.Name == "Complete");

            var request = requests.Single();

            Assert.NotNull(request.Context.Operation.Id);

            ValidateServiceBusRequest(request, true, _endpoint, _queueName, nameof(ServiceBusTrigger), null, null);
            ValidateServiceBusDependency(
                completeDependency,
                _endpoint, 
                _queueName, 
                "Complete",
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
            [ServiceBusTrigger(_queueName)] string message,
            MessageReceiver messageReceiver,
            string lockToken,
            TextWriter logger)
        {
            try
            {
                logger.WriteLine($"C# script processed queue message: '{message}'");

                if (message == "throw")
                {
                    throw new InvalidOperationException("boom!");
                }
            }
            finally
            {
                await messageReceiver.CompleteAsync(lockToken);
                _functionWaitHandle.Set();
            }
        }

        private void ValidateServiceBusRequest(
            RequestTelemetry request,
            bool success,
            string endpoint,
            string queueName,
            string operationName,
            string operationId,
            string parentId)
        {
            Assert.Equal($"type:Azure Service Bus | name:{queueName} | endpoint:{endpoint}/", request.Source);
            Assert.Null(request.Url);

            Assert.True(request.Properties.ContainsKey(LogConstants.FunctionExecutionTimeKey));
            Assert.True(double.TryParse(request.Properties[LogConstants.FunctionExecutionTimeKey], out double functionDuration));
            Assert.True(request.Duration.TotalMilliseconds >= functionDuration);

            TelemetryValidationHelpers.ValidateRequest(request, operationName, operationName, operationId, parentId, LogCategories.Results,
                success ? LogLevel.Information : LogLevel.Error, success);
        }

        private void ValidateServiceBusDependency(
            DependencyTelemetry dependency,
            string endpoint,
            string queueName,
            string name,
            string operationName,
            string operationId,
            string parentId,
            string category)
        {
            Assert.Equal($"{endpoint}/ | {queueName}", dependency.Target);
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
                    b.AddAzureStorage();
                    b.AddServiceBus(o =>
                    {
                        // We'll complete these ourselves as we don't
                        // want failures constantly retrying.
                        o.MessageHandlerOptions.AutoComplete = false;
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
            var messageReceiver = new MessageReceiver(_connectionString, _queueName, ReceiveMode.ReceiveAndDelete);
            int count = 0;
            do
            {
                var message = await messageReceiver.ReceiveAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
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
    }
}