﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    /// <summary>
    /// Tests for the lease expiration tests
    /// </summary>
    public class LeaseExpirationTests : IDisposable
    {
        private const string TestArtifactPrefix = "e2etestqueueleaserenew";
        private const string TestQueueName = TestArtifactPrefix + "%rnd%";

        private static bool _messageFoundAgain;

        private static CancellationTokenSource _tokenSource;
        private readonly CloudQueueClient _queueClient;
        private readonly CloudQueue _queue;
        private readonly JobHostOptions _options;

        public LeaseExpirationTests()
        {
            RandomNameResolver nameResolver = new RandomNameResolver();
            _options = new JobHostOptions()
            {
                // NameResolver = nameResolver,
                // TODO: DI:
                //TypeLocator = new FakeTypeLocator(typeof(LeaseExpirationTests))
            };

            // TODO: DI:
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(null);//_config.StorageConnectionString);
            _queueClient = storageAccount.CreateCloudQueueClient();

            _queue = _queueClient.GetQueueReference(nameResolver.ResolveInString(TestQueueName));
            _queue.CreateIfNotExistsAsync().Wait();
        }

        /// <summary>
        /// The function used to test the renewal. Whenever it is invoked, a counter increases
        /// </summary>
        public async static Task QueueMessageLeaseRenewFunction([QueueTrigger(TestQueueName)] string message,
            [Queue("queueleaserenew")]CloudQueue queueleaserenew)
        {
            // The time when the function will stop if the message is not found again
            DateTime endTime = DateTime.Now.AddMinutes(15);

            while (DateTime.Now < endTime)
            {
                CloudQueueMessage queueMessage = await queueleaserenew.GetMessageAsync();
                if (queueMessage != null)
                {
                    _messageFoundAgain = true;
                    break;
                }

                Thread.Sleep(TimeSpan.FromSeconds(30));
            }

            _tokenSource.Cancel();
        }

        /// <summary>
        /// There is a function that takes > 10 minutes and listens to a queue.
        /// </summary>
        /// <remarks>Ignored because it takes a long time. Can be enabled on demand</remarks>
        // Uncomment the Fact attribute to run
        //[Fact(Timeout = 20 * 60 * 1000)]
        public async Task QueueMessageLeaseRenew()
        {
            _messageFoundAgain = false;

            await _queue.AddMessageAsync(new CloudQueueMessage("Test"));

            _tokenSource = new CancellationTokenSource();
            JobHost host = new JobHost(new OptionsWrapper<JobHostOptions>(_options), new Mock<IJobHostContextFactory>().Object);

            _tokenSource.Token.Register(host.Stop);
            host.RunAndBlock();

            Assert.False(_messageFoundAgain);
        }

        public void Dispose()
        {
            foreach (var testqueue in _queueClient.ListQueuesSegmentedAsync(TestArtifactPrefix, null).Result.Results)
            {
                testqueue.DeleteAsync().Wait();
            }
        }
    }
}
