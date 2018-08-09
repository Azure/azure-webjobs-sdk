﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.WindowsAzure.Storage.Queue;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    public class ParallelExecutionTests : IDisposable
    {
        private const string TestArtifactPrefix = "e2etestparallelqueue";
        private const string TestQueueName = TestArtifactPrefix + "-%rnd%";

        private static readonly object _lock = new object();

        private static int _numberOfQueueMessages;
        private static int _receivedMessages;

        private static int _currentSimultaneouslyRunningFunctions;
        private static int _maxSimultaneouslyRunningFunctions;

        private static ManualResetEvent _allMessagesProcessed;
        private CloudQueueClient _queueClient;

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
        [InlineData(1, 1)]
        // Odd and even values
        [InlineData(2, 3)]
        [InlineData(3, 3)]
        public async Task MaxDegreeOfParallelism_Queues(int batchSize, int maxExpectedParallelism)
        {
            _receivedMessages = 0;
            _currentSimultaneouslyRunningFunctions = 0;
            _maxSimultaneouslyRunningFunctions = 0;
            _numberOfQueueMessages = batchSize * 3;

            RandomNameResolver nameResolver = new RandomNameResolver();
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<ParallelExecutionTests>(b =>
                {
                    b.AddAzureStorage();
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton<INameResolver>(nameResolver);
                    services.Configure<QueuesOptions>(o => o.BatchSize = batchSize);
                })
                .Build();

            StorageAccount storageAccount = host.GetStorageAccount();
            _queueClient = storageAccount.CreateCloudQueueClient();
            CloudQueue queue = _queueClient.GetQueueReference(nameResolver.ResolveInString(TestQueueName));

            await queue.CreateIfNotExistsAsync();

            for (int i = 0; i < _numberOfQueueMessages; i++)
            {
                int sleepTimeInSeconds = i % 2 == 0 ? 5 : 1;
                await queue.AddMessageAsync(new CloudQueueMessage(sleepTimeInSeconds.ToString()));
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
        }

        public void Dispose()
        {
            if (_queueClient != null)
            {
                foreach (var testQueue in _queueClient.ListQueuesSegmentedAsync(TestArtifactPrefix, null).Result.Results)
                {
                    testQueue.DeleteAsync().Wait();
                }
            }
        }
    }
}