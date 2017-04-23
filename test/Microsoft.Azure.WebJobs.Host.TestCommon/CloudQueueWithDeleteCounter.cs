using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using System.Threading;

namespace Microsoft.Azure.WebJobs.Host.TestCommon
{
    public class CloudQueueWithDeleteCounter:CloudQueue
    {
        public int DeleteCount = 0;
        public CloudQueueWithDeleteCounter(Uri uri):base(uri) { }
        public CloudQueueWithDeleteCounter(StorageUri uri, StorageCredentials creds ):base(uri,creds) { }
        public CloudQueueWithDeleteCounter(Uri uri, StorageCredentials creds) : base(uri, creds) { }

        public async override Task DeleteMessageAsync(CloudQueueMessage message, CancellationToken token)
        {
            DeleteCount++;
            await base.DeleteMessageAsync(message,token);
            return;
        }

    }
}
