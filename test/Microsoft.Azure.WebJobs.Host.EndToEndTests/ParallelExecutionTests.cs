// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    public class ParallelExecutionTests : IAsyncLifetime
    {
        private const string TestArtifactPrefix = "e2etestparallelqueue";
        private const string TestQueueName = TestArtifactPrefix + "-%rnd%";

        private static readonly object _lock = new object();

        private static int _numberOfQueueMessages;
        private static int _receivedMessages;

        private static int _currentSimultaneouslyRunningFunctions;
        private static int _maxSimultaneouslyRunningFunctions;

        private static ManualResetEvent _allMessagesProcessed;
        private QueueServiceClient _queueServiceClient;

        public static async Task ParallelQueueTrigger([QueueTrigger(TestQueueName)] int sleepTimeInSeconds)
        {
            lock (_lock)
            {
                _receivedMessages++;
                _currentSimultaneouslyRunningFunctions++;
                if (_currentSimultaneouslyRunningFunctions > _maxSimultaneouslyRunningFunctions)
                {
                    _maxSimultaneouslyRunningFunctions = _currentSimultaneouslyRunningFunctions;
                }
            }

            await Task.Delay(sleepTimeInSeconds * 1000);

            lock (_lock)
            {
                _currentSimultaneouslyRunningFunctions--;
                if (_receivedMessages == _numberOfQueueMessages)
                {
                    _allMessagesProcessed.Set();
                }
            }
        }

        [Theory]
        // One is special case (the old behaviour)
        [InlineData(1, 0, false)]
        // Odd and even values
        [InlineData(2, 0, false)]
        [InlineData(3, 0, false)]
        // One is special case (the old behaviour)
        [InlineData(1, 1, true)]
        // Odd and even values
        [InlineData(2, 3, true)]
        [InlineData(3, 3, true)]
        public async Task MaxDegreeOfParallelism_Queues(int batchSize, int maxExpectedParallelism, bool isDynamicSku)
        {
            var processorCount = Extensions.Storage.Utility.GetProcessorCount();
            _receivedMessages = 0;
            _currentSimultaneouslyRunningFunctions = 0;
            _maxSimultaneouslyRunningFunctions = 0;
            _numberOfQueueMessages = batchSize * 3;

            if (!isDynamicSku)
            {
                maxExpectedParallelism = Math.Min(_numberOfQueueMessages, ((batchSize / 2) * processorCount) + batchSize);
            }
            else
            {
                Environment.SetEnvironmentVariable(Constants.AzureWebsiteSku, "Dynamic");
            }

            RandomNameResolver nameResolver = new RandomNameResolver();
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<ParallelExecutionTests>(b =>
                {
                    b.AddAzureStorageQueues(o =>
                    {
                        o.BatchSize = batchSize;
                        o.MessageEncoding = QueueMessageEncoding.None;
                        o.NewBatchThreshold = isDynamicSku ? (batchSize / 2) : (batchSize / 2) * processorCount; // To get around static isDynamicSku variable in T2 storage extensions
                    });
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton<INameResolver>(nameResolver);
                })
                .ConfigureAppConfiguration(c => c.AddEnvironmentVariables())
                .Build();

            _queueServiceClient = TestHelpers.GetTestQueueServiceClient();
            var queueClient = _queueServiceClient.GetQueueClient(nameResolver.ResolveInString(TestQueueName));
            await queueClient.CreateIfNotExistsAsync();

            for (int i = 0; i < _numberOfQueueMessages; i++)
            {
                int sleepTimeInSeconds = i % 2 == 0 ? 5 : 1;
                await queueClient.SendMessageAsync(sleepTimeInSeconds.ToString());
            }

            using (_allMessagesProcessed = new ManualResetEvent(initialState: false))
            using (host)
            {
                await host.StartAsync();
                _allMessagesProcessed.WaitOne(TimeSpan.FromSeconds(90));
                await host.StopAsync();
            }

            Assert.Equal(_numberOfQueueMessages, _receivedMessages);
            Assert.Equal(0, _currentSimultaneouslyRunningFunctions);

            // the actual value will vary sometimes based on the speed of the machine
            // running the test.
            int delta = _maxSimultaneouslyRunningFunctions - maxExpectedParallelism;
            Assert.True(delta == 0 || delta == 1, $"Expected delta of 0 or 1. Actual: {delta}.");
            if (isDynamicSku)
            {
                Environment.SetEnvironmentVariable(Constants.AzureWebsiteSku, string.Empty);
            }
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            await CleanQueuesAsync();
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
