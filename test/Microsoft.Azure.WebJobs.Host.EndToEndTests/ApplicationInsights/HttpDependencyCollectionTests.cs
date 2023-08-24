// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
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
    public class HttpDependencyCollectionTests : IAsyncLifetime
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

        private RandomNameResolver _resolver;
        private BlobServiceClient _blobServiceClient;
        private QueueServiceClient _queueServiceClient;
        private QueueClient _triggerQueueClient;

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

        // TODO: Analyze Track2 changes for differences in this test from Track1
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

            var leafDependencies = new List<DependencyTelemetry>();
            GetLeafDependenciesOfRequest(_channel.Telemetries.OfType<DependencyTelemetry>(), request.Id, leafDependencies);

            List<DependencyTelemetry> dependencies = _channel.Telemetries.OfType<DependencyTelemetry>().ToList();

            var containerDependencies = dependencies.Where(d => d.Properties.ContainsKey("Container")).ToList();
            List<DependencyTelemetry> inDependencies = containerDependencies
                .Where(d => d.Properties.Single(p => p.Key == "Container").Value == _inputContainerName).ToList();
            List<DependencyTelemetry> outDependencies = containerDependencies
                .Where(d => d.Properties.Single(p => p.Key == "Container").Value == _outputContainerName).ToList();

            // only bindings calls are reported
            Assert.Equal(containerDependencies.Count, inDependencies.Count + outDependencies.Count);

            foreach (var inputDep in inDependencies)
            {
                ValidateBlobDependency(
                    inputDep,
                    _inputContainerName,
                    "in",
                    nameof(BlobInputAndOutputBindings),
                    request.Context.Operation.Id,
                    inputDep.Context.Operation.ParentId); // ParentId won't be the parentId of the RequestTelemetry since it is Dependency Tree

                // Check that the ParentId for inputDep can be traced back to RequestTelemtry
                Assert.Contains(leafDependencies, d => d.Id == inputDep.Id);
            }

            foreach (var outputDep in outDependencies)
            {
                ValidateBlobDependency(
                    outputDep,
                    _outputContainerName,
                    "out",
                    nameof(BlobInputAndOutputBindings),
                    request.Context.Operation.Id,
                    outputDep.Context.Operation.ParentId); // ParentId won't be the parentId of the RequestTelemetry since it is Dependency Tree

                // Check that the ParentId for outputDep can be traced back to RequestTelemtry
                Assert.Contains(leafDependencies, d => d.Id == outputDep.Id);
            }

            // PUT container, HEAD blob, PUT lease, PUT content
            Assert.True(outDependencies.Select(d => d.Name).Distinct().Count() <= 4);
            // since there could be failures and retries, we should expect more

            // HEAD blob, GET blob
            Assert.True(inDependencies.Select(d => d.Name).Distinct().Count() >= 2);

            // NEED TO REEXAMINE WITH TRACK2 EXTENSIONS
            // since there could be failures and retries, we should expect more
            //Assert.True(outDependencies.Count <= 4);
            //Assert.True(inDependencies.Count >= 2);
        }

        private static void GetLeafDependenciesOfRequest(IEnumerable<DependencyTelemetry> dependencies, string currId, List<DependencyTelemetry> leafDependencies)
        {
            var children = dependencies.Where(d => d.Context.Operation.ParentId == currId);
            if (!children.Any())
            {
                leafDependencies.Add(dependencies.Single(d => d.Id == currId));
                return;
            }

            foreach (var dependency in children)
            {
                GetLeafDependenciesOfRequest(dependencies, dependency.Id, leafDependencies);
            }
        }

        [Fact(Skip = "Fails on ADO agent; investigate post-migration.")]
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
            Assert.Contains("https://www.microsoft.com", dependency.Data);
            Assert.Equal("GET /", dependency.Name);
        }

        [Fact]
        public async Task UserCodeHttpCallsAreReportedOnceWhenMultipleHostsAreActive()
        {
            string testName = nameof(UserCodeHttpCall);
            using (var host1 = ConfigureHost(LogLevel.Information))
            using (var host2 = ConfigureHost(LogLevel.Information))
            {
                await host1.StartAsync();
                await host2.StartAsync();

                await host1.GetJobHost()
                    .CallAsync(typeof(HttpDependencyCollectionTests).GetMethod(testName));

                _functionWaitHandle.WaitOne();
                await Task.Delay(1000);

                await host1.StopAsync();
                await host2.StopAsync();
            }

            Assert.Single(_channel.Telemetries.OfType<RequestTelemetry>());
            Assert.Single(_channel.Telemetries.OfType<DependencyTelemetry>());
        }

        // TODO: Analyze Track2 changes for differences in this test from Track1
        [Fact]
        public async Task BlobTriggerCallsAreReported()
        {
            using (var host = ConfigureHost(LogLevel.Information))
            {
                await host.StartAsync();

                // let host run for a while
                await Task.Delay(3000);
                Assert.Empty(_channel.Telemetries.OfType<DependencyTelemetry>());

                var containerClient = _blobServiceClient.GetBlobContainerClient(_triggerContainerName);
                await containerClient.CreateIfNotExistsAsync();
                var blobClient = containerClient.GetBlobClient("triggerBlob");
                blobClient.UploadTextAsync("TestData", overwrite: true).Wait();

                _functionWaitHandle.WaitOne();
                // let host run for a while to write output queue message
                await Task.Delay(1000);

                await host.StopAsync();
            }

            RequestTelemetry request = _channel.Telemetries.OfType<RequestTelemetry>().Single();

            var leafDependencies = new List<DependencyTelemetry>();
            GetLeafDependenciesOfRequest(_channel.Telemetries.OfType<DependencyTelemetry>(), request.Id, leafDependencies);

            List<DependencyTelemetry> dependencies = _channel.Telemetries.OfType<DependencyTelemetry>().ToList();

            var azureDependencies = dependencies.Where(d => d.Type.StartsWith("Azure")).ToList();
            List<DependencyTelemetry> blobDependencies = azureDependencies.Where(d => d.Type == "Azure blob").ToList();
            List<DependencyTelemetry> queueDependencies = azureDependencies.Where(d => d.Type == "Azure queue").ToList();

            Assert.True(azureDependencies.Count >= 3);

            // only bindings calls are reported
            Assert.Equal(azureDependencies.Count, blobDependencies.Count + queueDependencies.Count);

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
                    inputDep.Context.Operation.ParentId); // ParentId won't be the parentId of the RequestTelemetry since it is Dependency Tree

                // Check that the ParentId for outputDep can be traced back to RequestTelemtry
                Assert.Contains(leafDependencies, d => d.Id == inputDep.Id);
            }

            ValidateQueueDependency(
                queueDependencies.First(),
                _outputQueueName,
                nameof(BlobTrigger),
                request.Context.Operation.Id,
                queueDependencies.First().Context.Operation.ParentId); // ParentId won't be the parentId of the RequestTelemetry since it is Dependency Tree

            // Check that the ParentId for outputDep can be traced back to RequestTelemtry
            Assert.Contains(leafDependencies, d => d.Id == queueDependencies.First().Id);
        }

        // TODO: Analyze Track2 changes for differences in this test from Track1
        [Fact]
        public async Task QueueTriggerCallsAreReported()
        {
            using (var host = ConfigureHost(LogLevel.Information))
            {
                await host.StartAsync();

                // let host run for a while
                await Task.Delay(3000);
                Assert.Empty(_channel.Telemetries.OfType<DependencyTelemetry>());

                await _triggerQueueClient.SendMessageAsync("TestData");

                _functionWaitHandle.WaitOne();
                // let host run for a while to write output queue message
                await Task.Delay(1000);

                await host.StopAsync();
            }

            RequestTelemetry request = _channel.Telemetries.OfType<RequestTelemetry>().Single();

            var leafDependencies = new List<DependencyTelemetry>();
            GetLeafDependenciesOfRequest(_channel.Telemetries.OfType<DependencyTelemetry>(), request.Id, leafDependencies);

            List<DependencyTelemetry> dependencies = _channel.Telemetries.OfType<DependencyTelemetry>().ToList();

            var containerDependencies = dependencies.Where(d => d.Properties.ContainsKey("Container")).ToList();
            List<DependencyTelemetry> bindingsDependencies = containerDependencies
                .Where(d => d.Properties.Single(p => p.Key == LogConstants.CategoryNameKey).Value == LogCategories.Bindings).ToList();

            // only bindings calls are reported, queue read happens before that and is not reported
            Assert.True(containerDependencies.Count >= 4);

            // PUT container, HEAD blob, PUT lease, PUT content
            Assert.Equal(containerDependencies.Count, bindingsDependencies.Count);

            foreach (var outputDep in bindingsDependencies)
            {
                ValidateBlobDependency(
                    outputDep,
                    _outputContainerName,
                    "out1",
                    nameof(QueueTrigger),
                    request.Context.Operation.Id,
                    outputDep.Context.Operation.ParentId); // ParentId won't be the parentId of the RequestTelemetry since it is Dependency Tree

                // Check that the ParentId for outputDep can be traced back to RequestTelemtry
                Assert.Contains(leafDependencies, d => d.Id == outputDep.Id);
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
                await httpClient.GetAsync("http://microsoft.com/").ContinueWith(t => { });
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
                    // Necessary for Blob/Queue bindings
                    b.AddAzureStorageBlobs();
                    // Track2 Queues Extensions expects Base64 encoded message; Track2 Queues package no longer encodes by default (Track1 used Base64 default)
                    b.AddAzureStorageQueues(o => o.MessageEncoding = QueueMessageEncoding.None);
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
                    b.AddApplicationInsightsWebJobs(o => o.InstrumentationKey = _mockApplicationInsightsKey);
                })
                .Build();

            TelemetryConfiguration telemteryConfiguration = host.Services.GetService<TelemetryConfiguration>();
            telemteryConfiguration.TelemetryChannel = _channel;

            var configuration = host.Services.GetService<IConfiguration>();

            _blobServiceClient = TestHelpers.GetTestBlobServiceClient();
            _queueServiceClient = TestHelpers.GetTestQueueServiceClient();

            _inputContainerName = _resolver.ResolveInString(InputContainerNamePattern);
            _outputContainerName = _resolver.ResolveInString(OutputContainerNamePattern);
            _triggerContainerName = _resolver.ResolveInString(TriggerContainerNamePattern);
            _outputQueueName = _resolver.ResolveInString(OutputQueueNamePattern);
            _triggerQueueName = _resolver.ResolveInString(TriggerQueueNamePattern);

            var inContainer = _blobServiceClient.GetBlobContainerClient(_inputContainerName);
            var outContainer = _blobServiceClient.GetBlobContainerClient(_outputContainerName);
            var triggerContainer = _blobServiceClient.GetBlobContainerClient(_triggerContainerName);
            var outputQueue = _queueServiceClient.GetQueueClient(_outputQueueName);
            _triggerQueueClient = _queueServiceClient.GetQueueClient(_triggerQueueName);

            inContainer.CreateIfNotExists();
            outContainer.CreateIfNotExists();
            triggerContainer.CreateIfNotExists();
            outputQueue.CreateIfNotExists();
            _triggerQueueClient.CreateIfNotExists();

            var blobClient = inContainer.GetBlobClient("in");
            blobClient.UploadTextAsync("TestData", overwrite: true).Wait();

            return host;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            _channel?.Dispose();
            await CleanBlobsAsync();
            await CleanQueuesAsync();
        }

        private async Task CleanBlobsAsync()
        {
            if (_blobServiceClient != null)
            {
                await foreach (var testBlob in _blobServiceClient.GetBlobContainersAsync(prefix: TestArtifactPrefix))
                {
                    await _blobServiceClient.DeleteBlobContainerAsync(testBlob.Name);
                }
            }
        }

        private async Task CleanQueuesAsync()
        {
            if (_queueServiceClient != null)
            {
                await foreach (var testQueue in _queueServiceClient.GetQueuesAsync(prefix: TestArtifactPrefix))
                {
                    await _queueServiceClient.DeleteQueueAsync(testQueue.Name);
                }
            }
        }
    }
}
