using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Xunit;

namespace Microsoft.Azure.WebJobs
{
    public class FakeStorageAccountProvider : XStorageAccountProvider
    {
        private readonly XStorageAccount _account;

        public FakeStorageAccountProvider(XStorageAccount account)
            : base(null)
        {

        }
        public override XStorageAccount Get(string name)
        {
            return _account;
        }
    }

    public class XFakeStorageAccount : XStorageAccount
    {
        public XFakeStorageAccount()
        {
            // $$$ Mock this out? 
            var acs = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            if (acs == null)
            {
                Assert.False(true); // Missing connection string 
            }
            _account = CloudStorageAccount.Parse(acs);
        }
        public override CloudBlobClient CreateCloudBlobClient()
        {
            return base.CreateCloudBlobClient();
        }
    }

    // Helpeful test extensions 
    public static class XFakeStorageAccountExtensions
    {
        public static async Task AddQueueMessageAsync(this XStorageAccount account, CloudQueueMessage message, string queueName)
        {
            var client = account.CreateCloudQueueClient();
            var queue = client.GetQueueReference(queueName);
            await queue.CreateIfNotExistsAsync();
            await queue.ClearAsync();
            await queue.AddMessageAsync(message);            
        }
    }

}
