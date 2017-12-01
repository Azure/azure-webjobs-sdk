using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests
{
    public class DispatchQueueEndToEndTests : IDisposable
    {
        // tells you how many function with different arguments have been ran
        private static ConcurrentStringSet _funcInvokation;
        // shows the concurrent execution when using sharedQueue
        // also make it easy to debug
        private static ITestOutputHelper _output;
        private static Stopwatch _stopwatch = new Stopwatch();

        private JobHostConfiguration _hostConfiguration;
        private CloudQueue _sharedQueue;
        private CloudQueue _poisonQueue;

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
            _hostConfiguration = new JobHostConfiguration();
            _hostConfiguration.AddExtension(new DispatchQueueTestConfig());
        }

        [Fact]
        // same trigger type, multiple functions
        public async Task DispatchQueueBatchTriggerTest()
        {
            _hostConfiguration.TypeLocator = new FakeTypeLocator(typeof(SampleTrigger));
            // each test will have a unique hostId so that consecutive test run will not be affected by clean up code
            _hostConfiguration.HostId = "bttest";

            using (var host = new JobHost(_hostConfiguration))
            {
                _funcInvokation = new ConcurrentStringSet();

                host.Start();

                _stopwatch.Restart();

                int twoFuncCount = DispatchQueueTestConfig.batchSize * 2;
                await TestHelpers.Await(() => _funcInvokation.TotalAdd() >= twoFuncCount || _funcInvokation.HasDuplicate(),
                                        7000, 1000);

                // make sure each function is triggered once and only once
                Assert.Equal(twoFuncCount, _funcInvokation.TotalAdd());
                Assert.False(_funcInvokation.HasDuplicate());

                _stopwatch.Stop();
            }
        }

        [Fact]
        public async void PoisonQueueTest()
        {
            _hostConfiguration.TypeLocator = new FakeTypeLocator(typeof(SampleTriggerWithPoisonQueue));
            _hostConfiguration.HostId = "pqtest";

            using (var host = new JobHost(_hostConfiguration))
            {
                _funcInvokation = new ConcurrentStringSet();

                host.Start();

                _stopwatch.Restart();

                // this test takes long since it does at least 5 dequeue on the poison message
                // count retries caused by failures and poison queue function process
                int funcWithExceptionCount = DispatchQueueTestConfig.batchSize + _hostConfiguration.Queues.MaxDequeueCount;
                await TestHelpers.Await(() => _funcInvokation.TotalAdd() >= funcWithExceptionCount, 10000, 1000);

                Assert.Equal(funcWithExceptionCount, _funcInvokation.TotalAdd());
                Assert.True(_funcInvokation.HasDuplicate());
                Assert.True(SampleTriggerWithPoisonQueue.poisonQueueResult);

                _stopwatch.Stop();
            }
        }

        public void Dispose()
        {
            // each test will have a different hostId
            // and therefore a different sharedQueue and poisonQueue
            CloudStorageAccount sdkAccount = CloudStorageAccount.Parse(_hostConfiguration.StorageConnectionString);
            CloudQueueClient client = sdkAccount.CreateCloudQueueClient();
            _sharedQueue = client.GetQueueReference("azure-webjobs-shared-" + _hostConfiguration.HostId);
            _poisonQueue = client.GetQueueReference("azure-webjobs-poison-" + _hostConfiguration.HostId);
            _sharedQueue.DeleteIfExistsAsync().Wait();
            _poisonQueue.DeleteIfExistsAsync().Wait();
        }

        public class SampleTriggerWithPoisonQueue
        {
            public static bool poisonQueueResult = false;
            public void PoisonQueueTrigger([DispatchQueueTrigger]JObject json)
            {
                int order = json["order"].Value<int>();
                string funcSignature = "PoisonQueueTrigger arg: " + order;
                _funcInvokation.Add(funcSignature);
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
                    poisonQueueResult = true;
                }
                _funcInvokation.Add("PosionQueueProcess");
                _output.WriteLine("PoisonQueueProcess" + " elapsed time: " + _stopwatch.ElapsedMilliseconds + " ms");
            }
        }

        public class SampleTrigger
        {
            public void DispatchQueueTrigger([DispatchQueueTrigger]JObject json)
            {
                string funcSignature = "DispatchQueueTrigger arg: " + json["order"].Value<int>();
                _funcInvokation.Add(funcSignature);
                _output.WriteLine(funcSignature + " elapsed time: " + _stopwatch.ElapsedMilliseconds + " ms");
            }

            public void DispatchQueueTrigger2([DispatchQueueTrigger]JObject json)
            {
                string funcSignature = "DispatchQueueTrigger2 arg: " + json["order"].Value<int>();
                _funcInvokation.Add(funcSignature);
                _output.WriteLine(funcSignature + " elapsed time: " + _stopwatch.ElapsedMilliseconds + " ms");
            }
        }
    }
}
