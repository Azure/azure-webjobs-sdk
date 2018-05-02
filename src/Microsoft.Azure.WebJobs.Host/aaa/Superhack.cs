using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs
{
    // $$$ Should this be in Host Storage? 
    public class QueueMoniker
    {
        public string ConnectionString { get; set; }
        public string QueueName { get; set; }
    }

    public interface ISuperhack
    {
        QueueMoniker GetQueueReference(string queueName); // Storage accounts?

        // Host may use queues internally for distributing work items. 
        IAsyncCollector<T> GetQueueWriter<T>(QueueMoniker queue);

        IListener CreateQueueListenr(
            QueueMoniker queue, // queue to listen on
            QueueMoniker poisonQueue, // Message enqueue here if callback fails 
            Func<string, CancellationToken, Task<FunctionResult>> callback
            );
    }
}
