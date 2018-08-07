// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.Azure.WebJobs
{
    static class MoreStorageExtensions
    {
        public static StorageAccount GetStorageAccount(this IHost host)
        {
            var provider = host.Services.GetRequiredService<StorageAccountProvider>(); // $$$ ok?
            return provider.GetHost();
        }

        public static async Task<CloudQueue> CreateQueueAsync(this StorageAccount account, string queueName)
        {
            CloudQueueClient client = account.CreateCloudQueueClient();
            CloudQueue queue = client.GetQueueReference(queueName);
            await queue.CreateIfNotExistsAsync();
            return queue;
        }

        public static async Task<CloudTable> CreateTableAsync(this StorageAccount account, string tableName)
        {
            CloudTableClient client = account.CreateCloudTableClient();
            CloudTable table = client.GetTableReference(tableName);
            await table.CreateIfNotExistsAsync();
            return table;
        }

        // $$$ Rationalize with AddFakeStorageAccountProvider in FunctionTests.
        public static IWebJobsBuilder UseFakeStorage(this IWebJobsBuilder builder)
        {
            return builder.UseStorage(new XFakeStorageAccount());
        }

        public static IWebJobsBuilder UseStorage(this IWebJobsBuilder builder, StorageAccount account)
        {
            builder.AddAzureStorage();
            builder.Services.Add(ServiceDescriptor.Singleton<StorageAccountProvider>(new FakeStorageAccountProvider(account)));

            return builder;
        }


        public static string DownloadText(this ICloudBlob blob)
        {
            if (blob == null)
            {
                throw new ArgumentNullException("blob");
            }

            using (Stream stream = blob.OpenReadAsync(CancellationToken.None).GetAwaiter().GetResult())
            using (TextReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        public static async Task UploadEmptyPageAsync(this CloudPageBlob blob)
        {
            if (blob == null)
            {
                throw new ArgumentNullException("blob");
            }

            using (CloudBlobStream stream = await blob.OpenWriteAsync(512))
            {
                await stream.CommitAsync();
            }
        }

        public static void InsertOrReplace(this CloudTable table, ITableEntity entity)
        {
            if (table == null)
            {
                throw new ArgumentNullException("table");
            }

            var operation = table.CreateInsertOrReplaceOperation(entity);
            table.ExecuteAsync(operation, CancellationToken.None).GetAwaiter().GetResult();
        }

        public static void Replace(this CloudTable table, ITableEntity entity)
        {
            if (table == null)
            {
                throw new ArgumentNullException("table");
            }

            var operation = table.CreateReplaceOperation(entity);
            table.ExecuteAsync(operation, CancellationToken.None).GetAwaiter().GetResult();
        }

        public static void Insert(this CloudTable table, ITableEntity entity)
        {
            if (table == null)
            {
                throw new ArgumentNullException("table");
            }

            var operation = table.CreateInsertOperation(entity);
            table.ExecuteAsync(operation, CancellationToken.None).GetAwaiter().GetResult();
        }

        public static TElement Retrieve<TElement>(this CloudTable table, string partitionKey, string rowKey)
            where TElement : ITableEntity, new()
        {
            if (table == null)
            {
                throw new ArgumentNullException("table");
            }

            var operation = table.CreateRetrieveOperation<TElement>(partitionKey, rowKey);
            TableResult result = table.ExecuteAsync(operation, CancellationToken.None).GetAwaiter().GetResult();
            return (TElement)result.Result;
        }
    }
}
