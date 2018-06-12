// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests
{
    public class BlobTests
    {
        private const string TriggerQueueName = "input";
        private const string ContainerName = "container";
        private const string BlobName = "blob";
        private const string BlobPath = ContainerName + "/" + BlobName;

        [Fact]
        public async Task Blob_IfBoundToCloudBlockBlob_BindsAndCreatesContainerButNotBlob()
        {
            // Act
            var account = new XFakeStorageAccount();

            var prog = new BindToCloudBlockBlobProgram();
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<BindToCloudBlockBlobProgram>(prog)
                .AddStorageBindings()
                .UseStorage(account)
                .Build();

            var jobHost = host.GetJobHost<BindToCloudBlockBlobProgram>();
            await jobHost.CallAsync(nameof(BindToCloudBlockBlobProgram.Run));

            var result = prog.Result;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(BlobName, result.Name);
            Assert.NotNull(result.Container);
            Assert.Equal(ContainerName, result.Container.Name);
            CloudBlobContainer container = GetContainerReference(account, ContainerName);
            Assert.True(await container.ExistsAsync());
            CloudBlockBlob blob = container.GetBlockBlobReference(BlobName);
            Assert.False(await blob.ExistsAsync());
        }

        [Fact]
        public async Task Blob_IfBoundToTextWriter_CreatesBlob()
        {
            // Arrange
            const string expectedContent = "message";
            var account = CreateFakeStorageAccount();
            CloudQueue triggerQueue = CreateQueue(account, TriggerQueueName);
            await triggerQueue.AddMessageAsync(new CloudQueueMessage(expectedContent));

            // Act
            RunTrigger(account, typeof(BindToTextWriterProgram));

            // Assert
            CloudBlobContainer container = GetContainerReference(account, ContainerName);
            Assert.True(await container.ExistsAsync());
            CloudBlockBlob blob = container.GetBlockBlobReference(BlobName);
            Assert.True(await blob.ExistsAsync());
            string content = blob.DownloadText();
            Assert.Equal(expectedContent, content);
        }

        private static XStorageAccount CreateFakeStorageAccount()
        {
            return new XFakeStorageAccount();         
        }        

        private static CloudQueue CreateQueue(XStorageAccount account, string queueName)
        {
            var client = account.CreateCloudQueueClient();
            var queue = client.GetQueueReference(queueName);
            queue.CreateIfNotExistsAsync().GetAwaiter().GetResult();
            return queue;
        }

        private static CloudBlobContainer GetContainerReference(XStorageAccount account, string containerName)
        {
            var client = account.CreateCloudBlobClient();
            return client.GetContainerReference(ContainerName);
        }

        private static void RunTrigger(XStorageAccount account, Type programType)
        {
            FunctionalTest.RunTrigger(account, programType);
        }

        private static TResult RunTrigger<TResult>(XStorageAccount account, Type programType,
            Action<TaskCompletionSource<TResult>> setTaskSource)
        {
            return FunctionalTest.RunTrigger<TResult>(account, programType, setTaskSource);
        }

        private class BindToCloudBlockBlobProgram
        {
            public CloudBlockBlob Result { get; set; }

            public void Run(
                [Blob(BlobPath)] CloudBlockBlob blob)
            {
                this.Result = blob;
            }
        }

        private class BindToTextWriterProgram
        {
            public static void Run([QueueTrigger(TriggerQueueName)] string message,
                [Blob(BlobPath)] TextWriter blob)
            {
                blob.Write(message);
                blob.Flush();
            }
        }
    }
}
