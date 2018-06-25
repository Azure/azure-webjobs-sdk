// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    public class DispatchQueueEndToEndTests : IDisposable
    {
        // tells you how many function with different arguments have been ran
        private static ConcurrentStringSet _funcInvocation;

        // shows the concurrent execution when using sharedQueue
        // also make it easy to debug
        private static ITestOutputHelper _output;
        private static Stopwatch _stopwatch = new Stopwatch();

        // Each test should set this up; it will be used during cleanup.
        private IHost _host;

        // thin wrapper around concurrentDictionary
        private class ConcurrentStringSet
        {
            private ConcurrentDictionary<String, int> _internal = new ConcurrentDictionary<string, int>();
            private bool _duplicate = false;
            private int _total = 0;
            public void Add(string value)
            {
                _internal.AddOrUpdate(value, 1, (k, v) =>
                {
                    _duplicate = true;
                    return v + 1;
                });
                Interlocked.Increment(ref _total);
            }

            public int TotalAdd()
            {
                return _total;
            }

            public bool HasDuplicate()
            {
                return _duplicate;
            }
        }

        public DispatchQueueEndToEndTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task DispatchQueueBatchTriggerTest_InMemory()
        {
            // The InMemoryLoadBalancerQueue is registered by default
            await RunTest();

            Assert.IsType<InMemoryLoadBalancerQueue>(_host.Services.GetService<ILoadBalancerQueue>());
        }

        [Fact]
        public async Task DispatchQueueBatchTriggerTest_Storage()
        {
            await RunTest(hb => hb.AddAzureStorage());

            // This type is internal, so validate on name.
            string loadBalancerName = _host.Services.GetService<ILoadBalancerQueue>().GetType().Name;
            Assert.Equal("StorageLoadBalancerQueue", loadBalancerName);
        }

        public async Task RunTest(Action<IHostBuilder> configure = null)
        {
            IHostBuilder hostBuilder = new HostBuilder()
                .ConfigureDefaultTestHost<SampleTrigger>()
                .AddExtension<DispatchQueueTestConfig>()
                .ConfigureServices(services =>
                {
                    // each test will have a unique hostId so that consecutive test run will not be affected by clean up code
                    services.Configure<JobHostOptions>(o => o.HostId = "bttest");

                });

            configure?.Invoke(hostBuilder);

            _host = hostBuilder.Build();

            _funcInvocation = new ConcurrentStringSet();

            _host.Start();

            _stopwatch.Restart();

            int twoFuncCount = DispatchQueueTestConfig.BatchSize * 2;
            await TestHelpers.Await(
                () => _funcInvocation.TotalAdd() >= twoFuncCount || _funcInvocation.HasDuplicate(),
                timeout: 10000);

            // make sure each function is triggered once and only once
            Assert.Equal(twoFuncCount, _funcInvocation.TotalAdd());
            Assert.False(_funcInvocation.HasDuplicate());

            _stopwatch.Stop();
        }

        [Fact]
        public async Task PoisonQueueTest_Storage()
        {
            _host = new HostBuilder()
                .ConfigureDefaultTestHost<SampleTriggerWithPoisonQueue>()
                .AddAzureStorage()
                .AddExtension<DispatchQueueTestConfig>()
                .ConfigureServices(services =>
                {
                    // each test will have a unique hostId so that consecutive test run will not be affected by clean up code
                    services.Configure<JobHostOptions>(o => o.HostId = "pqtest");
                })
                .Build();

            _funcInvocation = new ConcurrentStringSet();
            _host.Start();
            _stopwatch.Restart();

            // this test takes long since it does at least 5 dequeue on the poison message
            // count retries caused by failures and poison queue function process
            int funcWithExceptionCount = DispatchQueueTestConfig.BatchSize + _host.GetOptions<JobHostQueuesOptions>().MaxDequeueCount;

            await TestHelpers.Await(
              () => _funcInvocation.TotalAdd() >= funcWithExceptionCount,
              timeout: 10000);

            Assert.Equal(funcWithExceptionCount, _funcInvocation.TotalAdd());
            Assert.True(_funcInvocation.HasDuplicate());
            Assert.True(SampleTriggerWithPoisonQueue.PoisonQueueResult);

            _stopwatch.Stop();
        }

        public void Dispose()
        {
            // each test will have a different hostId
            // and therefore a different sharedQueue and poisonQueue
            // InMemory test does not use a storage account
            CloudQueueClient client = _host.GetStorageAccount()?.CreateCloudQueueClient();

            if (client != null)
            {
                var sharedQueue = client.GetQueueReference("azure-webjobs-shared-" + _host.GetOptions<JobHostOptions>().HostId);
                var poisonQueue = client.GetQueueReference("azure-webjobs-poison-" + _host.GetOptions<JobHostOptions>().HostId);
                sharedQueue.DeleteIfExistsAsync().Wait();
                poisonQueue.DeleteIfExistsAsync().Wait();
            }

            _host.Dispose();
        }

        public class SampleTriggerWithPoisonQueue
        {
            public static bool PoisonQueueResult = false;
            public void PoisonQueueTrigger([DispatchQueueTrigger]JObject json)
            {
                int order = json["order"].Value<int>();
                string funcSignature = "PoisonQueueTrigger arg: " + order;
                _funcInvocation.Add(funcSignature);
                _output.WriteLine(funcSignature + " elapsed time: " + _stopwatch.ElapsedMilliseconds + " ms");
                if (order == 0)
                {
                    throw new Exception("Can't deal with zero :(");
                }
            }

            public void PosionQueueProcess([QueueTrigger("azure-webjobs-poison-pqtest")]JObject message)
            {
                string functionId = message["FunctionId"].Value<string>();
                int value = message["Data"]["order"].Value<int>();
                if (functionId.Contains("PoisonQueueTrigger") && value == 0)
                {
                    PoisonQueueResult = true;
                }
                _funcInvocation.Add("PosionQueueProcess");
                _output.WriteLine("PoisonQueueProcess" + " elapsed time: " + _stopwatch.ElapsedMilliseconds + " ms");
            }
        }

        public class SampleTrigger
        {
            public void DispatchQueueTrigger([DispatchQueueTrigger]JObject json)
            {
                string funcSignature = "DispatchQueueTrigger arg: " + json["order"].Value<int>();
                _funcInvocation.Add(funcSignature);
                _output.WriteLine(funcSignature + " elapsed time: " + _stopwatch.ElapsedMilliseconds + " ms");
            }

            public void DispatchQueueTrigger2([DispatchQueueTrigger]JObject json)
            {
                string funcSignature = "DispatchQueueTrigger2 arg: " + json["order"].Value<int>();
                _funcInvocation.Add(funcSignature);
                _output.WriteLine(funcSignature + " elapsed time: " + _stopwatch.ElapsedMilliseconds + " ms");
            }
        }
    }
}
