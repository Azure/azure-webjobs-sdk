using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs.Host.Hosting;
using Microsoft.Azure.WebJobs.Host.Listeners;

namespace SampleHost
{
    internal class DynamicListenerStatusProvider : IDynamicListenerStatusProvider
    {
        private readonly Dictionary<string, BlobClient> _blobClients = new Dictionary<string, BlobClient>(StringComparer.OrdinalIgnoreCase);

        public DynamicListenerStatusProvider()
        {
            InitializeDynamicListeners();
        }

        public bool IsDynamic(string functionId)
        {
            return _blobClients.ContainsKey(functionId);
        }

        public async Task<DynamicListenerStatus> GetStatusAsync(string functionId)
        {
            bool disabled = false;

            // check remote semaphore to see if listener should be enabled
            if (_blobClients.TryGetValue(functionId, out BlobClient blobClient))
            {
                disabled = await blobClient.ExistsAsync();
            }

            var status = new DynamicListenerStatus
            {
                IsEnabled = !disabled,
                NextInterval = TimeSpan.FromSeconds(1)
            };

            return status;
        }

        private void InitializeDynamicListeners()
        {
            var dynamicFunctionIds = new string[] { "SampleHost.Functions.ProcessWorkItem" };

            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            var blobServiceClient = new BlobServiceClient(connectionString);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient("function-control");

            foreach (var functionId in dynamicFunctionIds)
            {
                var blobClient = blobContainerClient.GetBlobClient(functionId);
                _blobClients.Add(functionId, blobClient);
            }
        }

        public void DisposeListener(string functionId, IListener listener)
        {
            listener.Dispose();
        }
    }
}
