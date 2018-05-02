using Microsoft.Azure.WebJobs.Host.Blobs;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Microsoft.Azure.WebJobs
{
    internal static class StorageExtensions2
    {
        public static string GetBlobPath(this ICloudBlob blob)
        {
            return ToBlobPath(blob).ToString();
        }

        public static BlobPath ToBlobPath(this ICloudBlob blob)
        {
            if (blob == null)
            {
                throw new ArgumentNullException("blob");
            }

            return new BlobPath(blob.Container.Name, blob.Name);
        }

        public static Task<TableResult> ExecuteAsync(this CloudTable table, TableOperation operation, CancellationToken cancellationToken)
        {
            return table.ExecuteAsync(operation, null, null, cancellationToken);
        }

        public static TableOperation CreateInsertOperation(this CloudTable _sdk, ITableEntity entity)
        {
            var sdkOperation = TableOperation.Insert(entity);
            return sdkOperation;
        }


        public static TableOperation CreateInsertOrReplaceOperation(this CloudTable _sdk, ITableEntity entity)
        {
            var sdkOperation = TableOperation.InsertOrReplace(entity);
            return sdkOperation;
        }

        public static TableOperation CreateReplaceOperation(this CloudTable _sdk, ITableEntity entity)
        {
            var sdkOperation = TableOperation.Replace(entity);
            return sdkOperation;
        }

        public static TableOperation CreateRetrieveOperation<TElement>(this CloudTable table, string partitionKey, string rowKey)
    where TElement : ITableEntity, new()
        {            
            return Retrieve<TElement>(partitionKey, rowKey);
        }

        public static TableOperation Retrieve<TElement>(string partitionKey, string rowKey)
    where TElement : ITableEntity, new()
        {
            TableOperation sdkOperation = TableOperation.Retrieve<TElement>(partitionKey, rowKey);
            return sdkOperation;
            //var resolver = new TypeEntityResolver<TElement>(); $$$ Not used?
            //return new StorageTableOperation(sdkOperation, partitionKey, rowKey, resolver);
        }

        public static Task<IList<TableResult>> ExecuteBatchAsync(this CloudTable _sdk, TableBatchOperation batch,
    CancellationToken cancellationToken)
        {
            return _sdk.ExecuteBatchAsync(batch, requestOptions: null, operationContext: null, cancellationToken: cancellationToken);
        }

        public static Task CreateIfNotExistsAsync(this CloudTable _sdk, CancellationToken cancellationToken)
        {
            return _sdk.CreateIfNotExistsAsync(requestOptions: null, operationContext: null, cancellationToken: cancellationToken);
        }
    }
}
