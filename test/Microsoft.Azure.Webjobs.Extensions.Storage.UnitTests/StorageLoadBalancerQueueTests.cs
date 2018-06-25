using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Queues.Listeners;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
using WebJobs.Extensions.Storage;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Storage.UnitTests
{
    public class StorageLoadBalancerQueueTests
    {
        [Fact]
        public async Task HotPathNotificationTest()
        {
            var loadBalancerQueue = new StorageLoadBalancerQueue(
                new FakeStorageAccountProvider(new XFakeStorageAccount()),
                new OptionsWrapper<JobHostQueuesOptions>(new JobHostQueuesOptions()),
                new Mock<IWebJobsExceptionHandler>().Object,
                new SharedQueueWatcher(),
                new LoggerFactory());

            int calls = 0;

            var listener = loadBalancerQueue.CreateQueueListener("test", "test-poision",
                (s, cancellationToken) =>
                {
                    calls++;
                    return Task.FromResult(new FunctionResult(true));
                });

            var queueWriter = loadBalancerQueue.GetQueueWriter<string>("test");

            await listener.StartAsync(CancellationToken.None);

            int max = 10;
            var enqueue = new List<Task>();
            for (int i = 0; i < max; i++)
            {
                JObject message = JObject.Parse("{count:" + i + "}");
                enqueue.Add(queueWriter.AddAsync(message.ToString(), CancellationToken.None));
            }

            await Task.WhenAll(enqueue);

            // wait for dequeue
            await TestHelpers.Await(() => calls >= max, 1000, 200);

            await listener.StopAsync(CancellationToken.None);

            Assert.Equal(max, calls);
        }
    }
}
