using System;
using BenchmarkDotNet.Attributes;
using Microsoft.Azure.Storage.Queue;
using Microsoft.Azure.WebJobs.Host.Queues;
using Newtonsoft.Json.Linq;

namespace Benchmarks
{
    [MemoryDiagnoser]
    public class WebjobsExtensionsStorage
    {
        readonly CloudQueueMessage _minimal;
        readonly CloudQueueMessage _moderate;

        public WebjobsExtensionsStorage()
        {
            var expected = Guid.NewGuid();
            var minimal = new JObject
            {
                {"$AzureWebJobsParentId", new JValue(expected.ToString())}
            };

            _minimal = new CloudQueueMessage(minimal.ToString());

            var moderate = new JObject
            {
                {"$AzureWebJobsParentId", new JValue(expected.ToString())},
                {"BlobName", new JValue("some/path/to/blob")},
                {"Date", new JValue(DateTime.Now)},
                {"Number", new JValue(1234324)},
            };

            _moderate = new CloudQueueMessage(moderate.ToString());
        }

        [Benchmark]
        public Guid? QueueCausalityManager_GetOwner_Minimal()
        {
            return QueueCausalityManager.GetOwner(_minimal);
        }
        
        [Benchmark]
        public Guid? QueueCausalityManager_GetOwner_Moderate()
        {
            return QueueCausalityManager.GetOwner(_moderate);
        }
    }
}