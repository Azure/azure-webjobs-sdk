﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Xunit;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests.ApplicationInsights
{
    public class HttpDependencyCollectionTests : IDisposable
    {
        private const string TestArtifactPrefix = "e2etestsai";
        private const string OutputQueueNamePattern = TestArtifactPrefix + "out%rnd%";
        private const string TriggerQueueNamePattern = TestArtifactPrefix + "trigger%rnd%";
        private const string InputContainerNamePattern = TestArtifactPrefix + "-in%rnd%";
        private const string OutputContainerNamePattern = TestArtifactPrefix + "-out%rnd%";
        private const string TriggerContainerNamePattern = TestArtifactPrefix + "-trigger%rnd%";
        private const string _mockApplicationInsightsKey = "some_key";
        private readonly TestTelemetryChannel _channel = new TestTelemetryChannel();

        private static readonly AutoResetEvent _functionWaitHandle = new AutoResetEvent(false);

        private string _inputContainerName;
        private string _outputContainerName;
        private string _triggerContainerName;
        private string _outputQueueName;
        private string _triggerQueueName;

        private CloudBlobClient _blobClient;
        private RandomNameResolver _resolver;
        private CloudQueueClient _queueClient;
        private CloudQueue _triggerQueue;

        [Fact]
        public async Task BindingsAreNotReportedWhenFiltered()
        {
            using (var host = ConfigureHost(LogLevel.Warning))
            {
                await host.StartAsync();
                await host.GetJobHost()
                    .CallAsync(typeof(HttpDependencyCollectionTests).GetMethod(nameof(BlobInputAndOutputBindings)));

                _functionWaitHandle.WaitOne();
                await Task.Delay(1000);

                await host.StopAsync();
            }

            // at least one in and one out
            Assert.Empty(_channel.Telemetries.OfType<DependencyTelemetry>().ToList());
        }

        [Fact]
        public async Task BlobBindingsAreReported()
        {
            using (var host = ConfigureHost(LogLevel.Information))
            {
                await host.StartAsync();
                await host.GetJobHost()
                    .CallAsync(typeof(HttpDependencyCollectionTests).GetMethod(nameof(BlobInputAndOutputBindings)));

                _functionWaitHandle.WaitOne();
                await Task.Delay(1000);

                await host.StopAsync();
            }

            RequestTelemetry request = _channel.Telemetries.OfType<RequestTelemetry>().Single(r => r.Name == nameof(BlobInputAndOutputBindings));
            List<DependencyTelemetry> dependencies = _channel.Telemetries.OfType<DependencyTelemetry>().ToList();

            List<DependencyTelemetry> inDependencies = dependencies
                .Where(d => d.Properties.Single(p => p.Key == "Container").Value == _inputContainerName).ToList();
            List<DependencyTelemetry> outDependencies = dependencies
                .Where(d => d.Properties.Single(p => p.Key == "Container").Value == _outputContainerName).ToList();

            // only bindings calls are reported
            Assert.Equal(dependencies.Count, inDependencies.Count + outDependencies.Count);

            foreach (var inputDep in inDependencies)
            {
                ValidateBlobDependency(
                    inputDep,
                    _inputContainerName,
                    "in",
                    nameof(BlobInputAndOutputBindings),
                    request.Context.Operation.Id,
                    request.Id);
            }

            foreach (var outputDep in outDependencies)
            {
                ValidateBlobDependency(
                    outputDep,
                    _outputContainerName,
                    "out",
                    nameof(BlobInputAndOutputBindings),
                    request.Context.Operation.Id,
                    request.Id);
            }


            // PUT conatiner, HEAD blob, PUT lease, PUT content
            // since there could be failures and retries, we should expect more
            Assert.True(outDependencies.Count <= 4);

            // HEAD blob, GET blob
            Assert.True(inDependencies.Count >= 2);
        }

        [Fact]
        public async Task UserCodeHttpCallsAreReported()
        {
            string testName = nameof(UserCodeHttpCall);
            using (var host = ConfigureHost(LogLevel.Information))
            {
                await host.StartAsync();
                await host.GetJobHost()
                    .CallAsync(typeof(HttpDependencyCollectionTests).GetMethod(testName));

                _functionWaitHandle.WaitOne();
                await Task.Delay(1000);

                await host.StopAsync();
            }

            RequestTelemetry request = _channel.Telemetries.OfType<RequestTelemetry>().Single();
            List<DependencyTelemetry> dependencies = _channel.Telemetries.OfType<DependencyTelemetry>().ToList();

            // exactly one
            Assert.Single(dependencies);

            DependencyTelemetry dependency = dependencies.Single();
            TelemetryValidationHelpers.ValidateHttpDependency(
                dependency,
                testName,
                request.Context.Operation.Id,
                request.Id,
                LogCategories.CreateFunctionCategory(testName));

            Assert.Equal("Http", dependency.Type);
            Assert.Equal("www.microsoft.com", dependency.Target);
            Assert.Equal("https://www.microsoft.com", dependency.Data);
            Assert.Equal("GET /", dependency.Name);
        }

        [Fact]
        public async Task BlobTriggerCallsAreReported()
        {
            using (var host = ConfigureHost(LogLevel.Information))
            {
                await host.StartAsync();

                // let host run for a while
                await Task.Delay(3000);
                Assert.Empty(_channel.Telemetries.OfType<DependencyTelemetry>());

                CloudBlobContainer container = _blobClient.GetContainerReference(_triggerContainerName);
                await container.CreateIfNotExistsAsync();

                CloudBlockBlob blob = container.GetBlockBlobReference("triggerBlob");
                await blob.UploadTextAsync("TestData");

                _functionWaitHandle.WaitOne();
                // let host run for a while to write output queue message
                await Task.Delay(1000);

                await host.StopAsync();
            }

            RequestTelemetry request = _channel.Telemetries.OfType<RequestTelemetry>().Single();
            List<DependencyTelemetry> dependencies = _channel.Telemetries.OfType<DependencyTelemetry>().ToList();

            List<DependencyTelemetry> blobDependencies = dependencies.Where(d => d.Type == "Azure blob").ToList();
            List<DependencyTelemetry> queueDependencies = dependencies.Where(d => d.Type == "Azure queue").ToList();

            Assert.True(dependencies.Count >= 3);

            // only bindings calls are reported
            Assert.Equal(dependencies.Count, blobDependencies.Count + queueDependencies.Count);

            // one HEAD, one GET to download blob
            Assert.True(blobDependencies.Count >= 2);

            // one http call to enqueue message 
            Assert.True(queueDependencies.Count >= 1);

            foreach (var inputDep in blobDependencies)
            {
                ValidateBlobDependency(
                    inputDep,
                    _triggerContainerName,
                    "triggerBlob",
                    nameof(BlobTrigger),
                    request.Context.Operation.Id,
                    request.Id);
            }

            ValidateQueueDependency(
                queueDependencies.First(),
                _outputQueueName,
                nameof(BlobTrigger),
                request.Context.Operation.Id,
                request.Id);
        }

        [Fact]
        public async Task QueueTriggerCallsAreReported()
        {
            using (var host = ConfigureHost(LogLevel.Information))
            {
                await host.StartAsync();

                // let host run for a while
                await Task.Delay(3000);
                Assert.Empty(_channel.Telemetries.OfType<DependencyTelemetry>());

                await _triggerQueue.AddMessageAsync(new CloudQueueMessage("TestData"));

                _functionWaitHandle.WaitOne();
                // let host run for a while to write output queue message
                await Task.Delay(1000);

                await host.StopAsync();
            }

            RequestTelemetry request = _channel.Telemetries.OfType<RequestTelemetry>().Single();
            List<DependencyTelemetry> dependencies = _channel.Telemetries.OfType<DependencyTelemetry>().ToList();

            List<DependencyTelemetry> bindingsDependencies = dependencies
                .Where(d => d.Properties.Single(p => p.Key == LogConstants.CategoryNameKey).Value == LogCategories.Bindings).ToList();

            // only bindings calls are reported, queue read happens before that and is not reported
            Assert.True(dependencies.Count >= 4);

            // PUT container, HEAD blob, PUT lease, PUT content
            Assert.Equal(dependencies.Count, bindingsDependencies.Count);

            foreach (var outputDep in bindingsDependencies)
            {
                ValidateBlobDependency(
                    outputDep,
                    _outputContainerName,
                    "out1",
                    nameof(QueueTrigger),
                    request.Context.Operation.Id,
                    request.Id);
            }
        }

        [NoAutomaticTrigger]
        public static void BlobInputAndOutputBindings(
            [Blob(InputContainerNamePattern + "/in")] string input,
            [Blob(OutputContainerNamePattern + "/out")] out string output)
        {
            output = input;
            _functionWaitHandle.Set();
        }


        [NoAutomaticTrigger]
        public static async Task UserCodeHttpCall()
        {
            using (HttpClient httpClient = new HttpClient())
            {
                // we don't really care about the result, so, we'll ignore all errors
                await httpClient.GetAsync("http://microsoft.com").ContinueWith(t => { });
            }
            _functionWaitHandle.Set();
        }

        public static void BlobTrigger(
            [BlobTrigger(TriggerContainerNamePattern + @"/{name}")] string input,
            [Queue(OutputQueueNamePattern)] out string output)
        {
            output = input;
            _functionWaitHandle.Set();
        }

        public static void QueueTrigger(
            [QueueTrigger(TriggerQueueNamePattern)] string input,
            [Blob(OutputContainerNamePattern + "/out1")] out string output)
        {
            output = input;
            _functionWaitHandle.Set();
        }

        public static void ServiceBusTrigger(
            [ServiceBusTrigger(TriggerQueueNamePattern)] string input,
            [ServiceBus(OutputQueueNamePattern)] out string output)
        {
            output = input;
            _functionWaitHandle.Set();
        }

        private void ValidateBlobDependency(
            DependencyTelemetry dependency,
            string containerName,
            string blobName,
            string operationName,
            string operationId,
            string requestId)
        {
            Assert.Equal("Azure blob", dependency.Type);
            Assert.Equal(containerName, dependency.Properties["Container"]);

            // container creation does not have blob info
            if (dependency.Properties.ContainsKey("Blob"))
            {
                Assert.Equal(blobName, dependency.Properties["Blob"]);
            }

            TelemetryValidationHelpers.ValidateHttpDependency(dependency, operationName, operationId, requestId, LogCategories.Bindings);
        }


        private void ValidateQueueDependency(
            DependencyTelemetry dependency,
            string queueName,
            string operationName,
            string operationId,
            string requestId)
        {
            Assert.Equal("Azure queue", dependency.Type);
            Assert.True(dependency.Name.EndsWith(queueName));

            TelemetryValidationHelpers.ValidateHttpDependency(dependency, operationName, operationId, requestId, LogCategories.Bindings);
        }

        public IHost ConfigureHost(LogLevel logLevel)
        {
            _resolver = new RandomNameResolver();

            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<HttpDependencyCollectionTests>(b =>
                {
                    b.AddAzureStorage();
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton<INameResolver>(_resolver);
                    services.Configure<FunctionResultAggregatorOptions>(o =>
                    {
                        o.IsEnabled = false;
                    });
                })
                .ConfigureLogging(b =>
                {
                    b.SetMinimumLevel(logLevel);
                    b.AddApplicationInsights(o => o.InstrumentationKey = _mockApplicationInsightsKey);
                })
                .Build();

            TelemetryConfiguration telemteryConfiguration = host.Services.GetService<TelemetryConfiguration>();
            telemteryConfiguration.TelemetryChannel = _channel;

            StorageAccountProvider provider = host.Services.GetService<StorageAccountProvider>();
            CloudStorageAccount storageAccount = provider.GetHost().SdkObject;
            _blobClient = storageAccount.CreateCloudBlobClient();
            _queueClient = storageAccount.CreateCloudQueueClient();

            _inputContainerName = _resolver.ResolveInString(InputContainerNamePattern);
            _outputContainerName = _resolver.ResolveInString(OutputContainerNamePattern);
            _triggerContainerName = _resolver.ResolveInString(TriggerContainerNamePattern);
            _outputQueueName = _resolver.ResolveInString(OutputQueueNamePattern);
            _triggerQueueName = _resolver.ResolveInString(TriggerQueueNamePattern);

            CloudBlobContainer inContainer = _blobClient.GetContainerReference(_inputContainerName);
            CloudBlobContainer outContainer = _blobClient.GetContainerReference(_outputContainerName);
            CloudBlobContainer triggerContainer = _blobClient.GetContainerReference(_triggerContainerName);
            CloudQueue outputQueue = _queueClient.GetQueueReference(_outputQueueName);
            _triggerQueue = _queueClient.GetQueueReference(_triggerQueueName);

            inContainer.CreateIfNotExistsAsync().Wait();
            outContainer.CreateIfNotExistsAsync().Wait();
            triggerContainer.CreateIfNotExistsAsync().Wait();
            outputQueue.CreateIfNotExistsAsync().Wait();
            _triggerQueue.CreateIfNotExistsAsync().Wait();

            CloudBlockBlob inBlob = inContainer.GetBlockBlobReference("in");
            inBlob.UploadTextAsync("TestData").Wait();

            return host;
        }

        public void Dispose()
        {
            _channel?.Dispose();
        }
    }
}
