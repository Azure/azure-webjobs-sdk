using Microsoft.Extensions.Hosting;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Reflection;

namespace Microsoft.Azure.WebJobs
{
    // $$$ Helpers for dealing with unit-testable Cloud sdk. 
    static class Mock
    {        
        public static BlobResultSegment NewBlobResultSegment(
            BlobContinuationToken continuationToken,
            IEnumerable<ICloudBlob> results
            )
        {
            throw new NotImplementedException();
        }

        public static CloudQueueMessage SetDequeueCount(this CloudQueueMessage msg, int value)
        {
            var prop = msg.GetType().GetProperty(nameof(CloudQueueMessage.DequeueCount),
                BindingFlags.Instance | BindingFlags.NonPublic);

            prop.SetValue(msg, value);
            return msg;
        }

        public static CloudQueueMessage SetExpirationTime(this CloudQueueMessage msg, DateTimeOffset? value)
        {
            var prop = msg.GetType().GetProperty(nameof(CloudQueueMessage.ExpirationTime),
                BindingFlags.Instance | BindingFlags.NonPublic);

            prop.SetValue(msg, value);
            return msg;
        }

        public static CloudQueueMessage SetId(this CloudQueueMessage msg, string value)
        {
            var prop = msg.GetType().GetProperty(nameof(CloudQueueMessage.Id),
                BindingFlags.Instance | BindingFlags.NonPublic);

            prop.SetValue(msg, value);
            return msg;
        }

        public static CloudQueueMessage SetInsertionTime(this CloudQueueMessage msg, DateTimeOffset? value)
        {
            var prop = msg.GetType().GetProperty(nameof(CloudQueueMessage.InsertionTime),
                BindingFlags.Instance | BindingFlags.NonPublic);

            prop.SetValue(msg, value);
            return msg;
        }

        public static CloudQueueMessage SetNextVisibleTime(this CloudQueueMessage msg, DateTimeOffset? value)
        {
            var prop = msg.GetType().GetProperty(nameof(CloudQueueMessage.NextVisibleTime),
                BindingFlags.Instance | BindingFlags.NonPublic);

            prop.SetValue(msg, value);
            return msg;
        }

        public static CloudQueueMessage SetPopReceipt(this CloudQueueMessage msg, string value)
        {
            var prop = msg.GetType().GetProperty(nameof(CloudQueueMessage.PopReceipt),
                BindingFlags.Instance | BindingFlags.NonPublic);

            prop.SetValue(msg, value);
            return msg;
        }
    }

    static class MoreStorageExtensions
    {
        // $$$ Rationalize with AddFakeStorageAccountProvider in FunctionTests.
        public static IHostBuilder UseFakeStorage(this IHostBuilder builder)
        {
            return builder.UseStorage(new XFakeStorageAccount());
        }

        public static IHostBuilder UseStorage(this IHostBuilder builder, XStorageAccount account)
        {
            builder.ConfigureServices(services =>
           {
               services.TryAddSingleton<XStorageAccountProvider>(new FakeStorageAccountProvider(account));
           });

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
