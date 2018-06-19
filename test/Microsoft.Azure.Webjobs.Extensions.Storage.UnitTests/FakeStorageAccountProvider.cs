// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.Azure.WebJobs
{
    public class FakeStorageAccountProvider : StorageAccountProvider
    {
        private readonly XStorageAccount _account;

        public FakeStorageAccountProvider(XStorageAccount account)
            : base(null)
        {
            this._account = account;
        }
        public override XStorageAccount Get(string name)
        {
            return _account;
        }
    }

    public class XFakeStorageAccount : XStorageAccount
    {
#if false
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
#else
        private FakeStorage.FakeAccount _account2 = new FakeStorage.FakeAccount();

        public override CloudQueueClient CreateCloudQueueClient()
        {
            return _account2.CreateCloudQueueClient();
        }
        public override CloudBlobClient CreateCloudBlobClient()
        {
            return _account2.CreateCloudBlobClient();
        }

        public override CloudTableClient CreateCloudTableClient()
        {
            return _account2.CreateCloudTableClient();
        }

        public override string Name => _account2.Name;
        public override bool IsDevelopmentStorageAccount() { return true; }

#endif
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
