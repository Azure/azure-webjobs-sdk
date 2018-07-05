// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private RandomNameResolver _resolver;
        private readonly TestTelemetryChannel _channel = new TestTelemetryChannel();
        private static readonly AutoResetEvent _functionWaitHandle = new AutoResetEvent(false);

        public ServiceBusRequestAndDependencyCollectionTests()
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            var configSection = Utility.GetExtensionConfigurationSection(config, "ServiceBus");
            _connectionString = configSection.GetConnectionString("Primary");

            var connStringBuilder = new ServiceBusConnectionStringBuilder(_connectionString);
            _endpoint = connStringBuilder.Endpoint;
        }

        [Fact]
        public async Task ServiceBusDepenedenciesAndRequestAreTracked()
        {
            using (var host = ConfigureHost())
            {
                await host.StartAsync();
                await host.GetJobHost()
                    .CallAsync(typeof(ServiceBusRequestAndDependencyCollectionTests).GetMethod(nameof(ServiceBusOut)), new {input = "message"});

                _functionWaitHandle.WaitOne();
                await Task.Delay(1000);

                await host.StopAsync();
            }

            List<RequestTelemetry> requests = _channel.Telemetries.OfType<RequestTelemetry>().ToList();
            List<DependencyTelemetry> dependencies = _channel.Telemetries.OfType<DependencyTelemetry>().ToList();

            Assert.Equal(2, requests.Count);
            Assert.Single(dependencies);

            Assert.Single(requests.Where(r => r.Context.Operation.ParentId == dependencies.Single().Id));
            var sbTriggerRequest = requests.Single(r => r.Context.Operation.ParentId == dependencies.Single().Id);
            var manualCallRequest = requests.Single(r => r.Name == nameof(ServiceBusOut));
            var sbOutDependency = dependencies.Single();

            string operationId = manualCallRequest.Context.Operation.Id;
            Assert.Equal(operationId, sbTriggerRequest.Context.Operation.Id);

            ValidateServiceBusDependency(sbOutDependency, _endpoint, _queueName, "Send", nameof(ServiceBusOut), operationId, manualCallRequest.Id);
            ValidateServiceBusRequest(sbTriggerRequest, _endpoint, _queueName, nameof(ServiceBusTrigger), operationId, sbOutDependency.Id);
        }

        [NoAutomaticTrigger]
        public static void ServiceBusOut(
            string input,
            [ServiceBus(_queueName)] out string message,
            TextWriter logger)
        {
            message = input;
        }

        public static void ServiceBusTrigger(
            [ServiceBusTrigger(_queueName)] string message,
            TextWriter logger)
        {
            logger.WriteLine($"C# script processed queue message: '{message}'");
            _functionWaitHandle.Set();
        }

        private void ValidateServiceBusRequest(
            RequestTelemetry request,
            string endpoint,
            string queueName,
            string operationName,
            string operationId,
            string parentId)
        {
            Assert.Equal($"type:Azure Service Bus | name:{queueName} | endpoint:{endpoint}/", request.Source);
            Assert.Null(request.Url);
            Assert.Equal(operationName, request.Name);

            Assert.True(request.Properties.ContainsKey(LogConstants.FunctionExecutionTimeKey));
            Assert.True(double.TryParse(request.Properties[LogConstants.FunctionExecutionTimeKey], out double functionDuration));
            Assert.True(request.Duration.TotalMilliseconds >= functionDuration);

            TelemetryValidationHelpers.ValidateRequest(request, operationName, operationId, parentId, LogCategories.Results);
        }

        private void ValidateServiceBusDependency(
            DependencyTelemetry dependency,
            string endpoint,
            string queueName,
            string name,
            string operationName,
            string operationId,
            string parentId)
        {
            Assert.Equal($"{endpoint}/ | {queueName}", dependency.Target);
            Assert.Equal("Azure Service Bus", dependency.Type);
            Assert.Equal(name, dependency.Name);
            Assert.True(dependency.Success);
            Assert.Null(dependency.Data);
            TelemetryValidationHelpers.ValidateDependency(dependency, operationName, operationId, parentId, LogCategories.Bindings);
        }

        public IHost ConfigureHost()
        {
            _resolver = new RandomNameResolver();

            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<ServiceBusRequestAndDependencyCollectionTests>(b =>
                {
                    b.AddAzureStorage();
                    b.AddServiceBus();
                })
                .ConfigureLogging(b =>
                {
                    b.SetMinimumLevel(LogLevel.Information);
                    b.AddApplicationInsights(o => o.InstrumentationKey = _mockApplicationInsightsKey);
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
    }
}
