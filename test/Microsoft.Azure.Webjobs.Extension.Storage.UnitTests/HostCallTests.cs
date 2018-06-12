// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Blobs;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests
{
    // $$$ - this should be split up into blob/table/queue 
    // Some tests in this class aren't as targeted as most other tests in this project.
    // (Look elsewhere for better examples to use as templates for new tests.)
    public class HostCallTests
    {
        private const string ContainerName = "container";
        private const string BlobName = "blob";
        private const string BlobPath = ContainerName + "/" + BlobName;
        private const string OutputBlobName = "blob.out";
        private const string OutputBlobPath = ContainerName + "/" + OutputBlobName;
        private const string QueueName = "input";
        private const string OutputQueueName = "output";
        private const string TableName = "Table";
        private const string PartitionKey = "PK";
        private const string RowKey = "RK";
        private const int TestValue = Int32.MinValue;
        private const string TestQueueMessage = "ignore";

        [Theory]
        [InlineData("FuncWithString")]
        [InlineData("FuncWithTextReader")]
        [InlineData("FuncWithStreamRead")]
        [InlineData("FuncWithBlockBlob")]
        [InlineData("FuncWithOutStringNull")]
        [InlineData("FuncWithT")]
        [InlineData("FuncWithOutTNull")]
        [InlineData("FuncWithValueT")]
        public async Task Blob_IfBoundToTypeAndBlobIsMissing_DoesNotCreate(string methodName)
        {
            // Arrange
            var account = CreateFakeStorageAccount();
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(ContainerName);
            CloudBlockBlob blob = container.GetBlockBlobReference(BlobName);

            // Act
            Call(account, typeof(MissingBlobProgram), methodName, typeof(MissingBlobToCustomObjectBinder),
                typeof(MissingBlobToCustomValueBinder));

            // Assert
            Assert.False(await blob.ExistsAsync());
        }

        [Theory]
        [InlineData("FuncWithOutString")]
        [InlineData("FuncWithStreamWriteNoop")]
        [InlineData("FuncWithTextWriter")]
        [InlineData("FuncWithStreamWrite")]
        [InlineData("FuncWithOutT")]
        [InlineData("FuncWithOutValueT")]
        public async Task Blob_IfBoundToTypeAndBlobIsMissing_Creates(string methodName)
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();
            var client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(ContainerName);
            CloudBlockBlob blob = container.GetBlockBlobReference(BlobName);

            // Act
            Call(account, typeof(MissingBlobProgram), methodName, typeof(MissingBlobToCustomObjectBinder),
                typeof(MissingBlobToCustomValueBinder));

            // Assert
            Assert.True(await blob.ExistsAsync());
        }

        [Fact]
        public async Task BlobTrigger_IfHasUnboundParameter_CanCall()
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(ContainerName);
            const string inputBlobName = "note-monday.csv";
            CloudBlockBlob inputBlob = container.GetBlockBlobReference(inputBlobName);
            await container.CreateIfNotExistsAsync();
            await inputBlob.UploadTextAsync("abc");

            IDictionary<string, object> arguments = new Dictionary<string, object>
            {
                { "values", ContainerName + "/" + inputBlobName },
                { "unbound", "test" }
            };

            // Act
            Call(account, typeof(BlobProgram), "UnboundParameter", arguments);

            CloudBlockBlob outputBlob = container.GetBlockBlobReference("note.csv");
            string content = outputBlob.DownloadText();
            Assert.Equal("done", content);

            // $$$ Put this in its own unit test?
            Guid? guid = BlobCausalityManager.GetWriterAsync(outputBlob,
                CancellationToken.None).GetAwaiter().GetResult();

            Assert.True(guid != Guid.Empty, "Blob is missing causality information");
        }

        [Fact]
        public async Task Blob_IfBoundToCloudBlockBlob_CanCall()
        {
            // Arrange
            var account = CreateFakeStorageAccount();
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(ContainerName);
            CloudBlockBlob inputBlob = container.GetBlockBlobReference(BlobName);
            await container.CreateIfNotExistsAsync();
            await inputBlob.UploadTextAsync("ignore");

            // Act
            Call(account, typeof(BlobProgram), "BindToCloudBlockBlob");
        }

        [Fact]
        public async Task Blob_IfBoundToString_CanCall()
        {
            // Arrange
            var account = CreateFakeStorageAccount();
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(ContainerName);
            CloudBlockBlob inputBlob = container.GetBlockBlobReference(BlobName);
            await container.CreateIfNotExistsAsync();
            await inputBlob.UploadTextAsync("0,1,2");

            Call(account, typeof(BlobProgram), "BindToString");
        }

        [Fact]
        public async Task Blob_IfCopiedViaString_CanCall()
        {
            // Arrange
            var account = CreateFakeStorageAccount();
            var client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(ContainerName);
            CloudBlockBlob inputBlob = container.GetBlockBlobReference(BlobName);
            await container.CreateIfNotExistsAsync();
            string expectedContent = "abc";
            await inputBlob.UploadTextAsync(expectedContent);

            // Act
            Call(account, typeof(BlobProgram), "CopyViaString");

            // Assert
            CloudBlockBlob outputBlob = container.GetBlockBlobReference(OutputBlobName);
            string outputContent = outputBlob.DownloadText();
            Assert.Equal(expectedContent, outputContent);
        }

        [Fact]
        public async Task BlobTrigger_IfCopiedViaTextReaderTextWriter_CanCall()
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(ContainerName);
            CloudBlockBlob inputBlob = container.GetBlockBlobReference(BlobName);
            await container.CreateIfNotExistsAsync();
            string expectedContent = "abc";
            await inputBlob.UploadTextAsync(expectedContent);

            // TODO: Remove argument once host.Call supports more flexibility.
            IDictionary<string, object> arguments = new Dictionary<string, object>
            {
                { "values", BlobPath }
            };

            // Act
            Call(account, typeof(BlobProgram), "CopyViaTextReaderTextWriter", arguments);

            // Assert
            CloudBlockBlob outputBlob = container.GetBlockBlobReference(OutputBlobName);
            string outputContent = outputBlob.DownloadText();
            Assert.Equal(expectedContent, outputContent);
        }

        [Fact]
        public async Task BlobTrigger_IfBoundToICloudBlob_CanCallWithBlockBlob()
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(ContainerName);
            CloudBlockBlob blob = container.GetBlockBlobReference(BlobName);
            await container.CreateIfNotExistsAsync();
            await blob.UploadTextAsync("ignore");

            // TODO: Remove argument once host.Call supports more flexibility.
            IDictionary<string, object> arguments = new Dictionary<string, object>
            {
                { "blob", BlobPath }
            };

            // Act
            ICloudBlob result = Call<ICloudBlob>(account, typeof(BlobTriggerBindToICloudBlobProgram), "Call", arguments,
                (s) => BlobTriggerBindToICloudBlobProgram.TaskSource = s);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(BlobType.BlockBlob, result.BlobType);
        }

        [Fact]
        public async Task BlobTrigger_IfBoundToICloudBlob_CanCallWithPageBlob()
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(ContainerName);
            CloudPageBlob blob = container.GetPageBlobReference(BlobName);
            await container.CreateIfNotExistsAsync();
            await blob.UploadEmptyPageAsync();

            // TODO: Remove argument once host.Call supports more flexibility.
            IDictionary<string, object> arguments = new Dictionary<string, object>
            {
                { "blob", BlobPath }
            };

            // Act
            ICloudBlob result = Call<ICloudBlob>(account, typeof(BlobTriggerBindToICloudBlobProgram), "Call", arguments,
                (s) => BlobTriggerBindToICloudBlobProgram.TaskSource = s);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(BlobType.PageBlob, result.BlobType);
        }

        [Fact]
        public void BlobTrigger_IfBoundToICloudBlobAndTriggerArgumentIsMissing_CallThrows()
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();

            // Act
            Exception exception = CallFailure(account, typeof(BlobTriggerBindToICloudBlobProgram), "Call");

            // Assert
            Assert.IsType<InvalidOperationException>(exception);
            Assert.Equal("Missing value for trigger parameter 'blob'.", exception.Message);
        }

        [Fact]
        public async Task BlobTrigger_IfBoundToCloudBlockBlob_CanCall()
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(ContainerName);
            CloudBlockBlob blob = container.GetBlockBlobReference(BlobName);
            await container.CreateIfNotExistsAsync();
            await blob.UploadTextAsync("ignore");

            // TODO: Remove argument once host.Call supports more flexibility.
            IDictionary<string, object> arguments = new Dictionary<string, object>
            {
                { "blob", BlobPath }
            };

            // Act
            CloudBlockBlob result = Call<CloudBlockBlob>(account, typeof(BlobTriggerBindToCloudBlockBlobProgram),
                "Call", arguments, (s) => BlobTriggerBindToCloudBlockBlobProgram.TaskSource = s);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void BlobTrigger_IfBoundToCloudBLockBlobAndTriggerArgumentIsMissing_CallThrows()
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();

            // Act
            Exception exception = CallFailure(account, typeof(BlobTriggerBindToCloudBlockBlobProgram), "Call");

            // Assert
            Assert.IsType<InvalidOperationException>(exception);
            Assert.Equal("Missing value for trigger parameter 'blob'.", exception.Message);
        }

        private class BlobTriggerBindToCloudBlockBlobProgram
        {
            public static TaskCompletionSource<CloudBlockBlob> TaskSource { get; set; }

            public static void Call([BlobTrigger(BlobPath)] CloudBlockBlob blob)
            {
                TaskSource.TrySetResult(blob);
            }
        }

        [Fact]
        public async Task BlobTrigger_IfBoundToCloudPageBlob_CanCall()
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(ContainerName);
            CloudPageBlob blob = container.GetPageBlobReference(BlobName);
            await container.CreateIfNotExistsAsync();
            await blob.UploadEmptyPageAsync();

            // TODO: Remove argument once host.Call supports more flexibility.
            IDictionary<string, object> arguments = new Dictionary<string, object>
            {
                { "blob", BlobPath }
            };

            // Act
            CloudPageBlob result = Call<CloudPageBlob>(account, typeof(BlobTriggerBindToCloudPageBlobProgram), "Call",
                arguments, (s) => BlobTriggerBindToCloudPageBlobProgram.TaskSource = s);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void BlobTrigger_IfBoundToCloudPageBlobAndTriggerArgumentIsMissing_CallThrows()
        {
            // Arrange
            var account = CreateFakeStorageAccount();

            // Act
            Exception exception = CallFailure(account, typeof(BlobTriggerBindToCloudPageBlobProgram), "Call");

            // Assert
            Assert.IsType<InvalidOperationException>(exception);
            Assert.Equal("Missing value for trigger parameter 'blob'.", exception.Message);
        }

        private class BlobTriggerBindToCloudPageBlobProgram
        {
            public static TaskCompletionSource<CloudPageBlob> TaskSource { get; set; }

            public static void Call([BlobTrigger(BlobPath)] CloudPageBlob blob)
            {
                TaskSource.TrySetResult(blob);
            }
        }

        [Fact]
        public async Task BlobTrigger_IfBoundToCloudAppendBlob_CanCall()
        {
            // Arrange
            var account = CreateFakeStorageAccount();
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(ContainerName);
            CloudAppendBlob blob = container.GetAppendBlobReference(BlobName);
            await container.CreateIfNotExistsAsync();
            await blob.UploadTextAsync("test");

            // TODO: Remove argument once host.Call supports more flexibility.
            IDictionary<string, object> arguments = new Dictionary<string, object>
            {
                { "blob", BlobPath }
            };

            // Act
            CloudAppendBlob result = Call<CloudAppendBlob>(account, typeof(BlobTriggerBindToCloudAppendBlobProgram), "Call",
                arguments, (s) => BlobTriggerBindToCloudAppendBlobProgram.TaskSource = s);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void BlobTrigger_IfBoundToCloudAppendBlobAndTriggerArgumentIsMissing_CallThrows()
        {
            // Arrange
            var account = CreateFakeStorageAccount();

            // Act
            Exception exception = CallFailure(account, typeof(BlobTriggerBindToCloudAppendBlobProgram), "Call");

            // Assert
            Assert.IsType<InvalidOperationException>(exception);
            Assert.Equal("Missing value for trigger parameter 'blob'.", exception.Message);
        }

        private class BlobTriggerBindToCloudAppendBlobProgram
        {
            public static TaskCompletionSource<CloudAppendBlob> TaskSource { get; set; }

            public static void Call([BlobTrigger(BlobPath)] CloudAppendBlob blob)
            {
                TaskSource.TrySetResult(blob);
            }
        }

        [Fact]
        public void Int32Argument_CanCallViaStringParse()
        {
            // Arrange
            var account = CreateFakeStorageAccount();
            IDictionary<string, object> arguments = new Dictionary<string, object>
            {
                { "value", "15" }
            };

            // Act
            int result = Call<int>(account, typeof(UnboundInt32Program), "Call", arguments,
                (s) => UnboundInt32Program.TaskSource = s);

            Assert.Equal(15, result);
        }

        private class UnboundInt32Program
        {
            public static TaskCompletionSource<int> TaskSource { get; set; }

            [NoAutomaticTrigger]
            public static void Call(int value)
            {
                TaskSource.TrySetResult(value);
            }
        }

        [Fact]
        public void CloudStorageAccount_CanCall()
        {
            // Arrange
            var account = CreateFakeStorageAccount();

            // Act
            CloudStorageAccount result = Call<CloudStorageAccount>(account, typeof(CloudStorageAccountProgram),
                "BindToCloudStorageAccount", (s) => CloudStorageAccountProgram.TaskSource = s);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(account.BlobEndpoint, result.BlobEndpoint);
        }

        private class CloudStorageAccountProgram
        {
            public static TaskCompletionSource<CloudStorageAccount> TaskSource { get; set; }

            [NoAutomaticTrigger]
            public static void BindToCloudStorageAccount(CloudStorageAccount account)
            {
                TaskSource.TrySetResult(account);
            }
        }

        [Fact]
        public void Queue_IfBoundToOutPoco_CanCall()
        {
            var account = CreateFakeStorageAccount();

            // Act
            Call(account, typeof(QueueProgram), "BindToOutPoco");

            // Assert
            var queue = account.CreateCloudQueueClient().GetQueueReference(OutputQueueName);
            AssertMessageSent(new PocoMessage { Value = "15" }, queue);
        }

        [Fact]
        public async Task Queue_IfBoundToICollectorPoco_CanCall()
        {
            await TestEnqueueMultiplePocoMessages("BindToICollectorPoco");
        }

        [Fact]
        public async Task Queue_IfBoundToIAsyncCollectorPoco_CanCall()
        {
            await TestEnqueueMultiplePocoMessages("BindToIAsyncCollectorPoco");
        }

        [Fact]
        public async Task Queue_IfBoundToIAsyncCollectorByteArray_CanCall()
        {
            var account = CreateFakeStorageAccount();

            // Act
            Call(account, typeof(QueueProgram), "BindToIAsyncCollectorByteArray");

            // Assert
            var queue = account.CreateCloudQueueClient().GetQueueReference(OutputQueueName);
            IEnumerable<CloudQueueMessage> messages = await queue.GetMessagesAsync(messageCount: int.MaxValue);
            Assert.NotNull(messages);
            Assert.Equal(3, messages.Count());
            CloudQueueMessage[] sortedMessages = messages.OrderBy((m) => m.AsString).ToArray();

            Assert.Equal(Encoding.UTF8.GetBytes("test1"), sortedMessages[0].AsBytes);
            Assert.Equal(Encoding.UTF8.GetBytes("test2"), sortedMessages[1].AsBytes);
            Assert.Equal(Encoding.UTF8.GetBytes("test3"), sortedMessages[2].AsBytes);
        }

        [Fact]
        public async Task Queue_IfBoundToICollectorByteArray_CanCall()
        {
            var account = CreateFakeStorageAccount();

            // Act
            Call(account, typeof(QueueProgram), "BindToICollectorByteArray");

            // Assert
            CloudQueue queue = account.CreateCloudQueueClient().GetQueueReference(OutputQueueName);
            IEnumerable<CloudQueueMessage> messages = await queue.GetMessagesAsync(messageCount: int.MaxValue);
            Assert.NotNull(messages);
            Assert.Equal(3, messages.Count());
            CloudQueueMessage[] sortedMessages = messages.OrderBy((m) => m.AsString).ToArray();

            Assert.Equal(Encoding.UTF8.GetBytes("test1"), sortedMessages[0].AsBytes);
            Assert.Equal(Encoding.UTF8.GetBytes("test2"), sortedMessages[1].AsBytes);
            Assert.Equal(Encoding.UTF8.GetBytes("test3"), sortedMessages[2].AsBytes);
        }

        [Fact]
        public void Queue_IfBoundToIAsyncCollectorInt_NotSupported()
        {
            XStorageAccount account = CreateFakeStorageAccount();

            // Act
            FunctionIndexingException ex = Assert.Throws<FunctionIndexingException>(() =>
            {
                Call(account, typeof(QueueNotSupportedProgram), "BindToICollectorInt");
            });

            // Assert
            Assert.Equal("Primitive types are not supported.", ex.InnerException.Message);
        }

        private static async Task TestEnqueueMultiplePocoMessages(string methodName)
        {
            XStorageAccount account = CreateFakeStorageAccount();

            // Act
            Call(account, typeof(QueueProgram), methodName);

            // Assert
            CloudQueue queue = account.CreateCloudQueueClient().GetQueueReference(OutputQueueName);
            IEnumerable<CloudQueueMessage> messages = await queue.GetMessagesAsync(messageCount: int.MaxValue);
            Assert.NotNull(messages);
            Assert.Equal(3, messages.Count());
            IEnumerable<CloudQueueMessage> sortedMessages = messages.OrderBy((m) => m.AsString);
            CloudQueueMessage firstMessage = sortedMessages.ElementAt(0);
            CloudQueueMessage secondMessage = sortedMessages.ElementAt(1);
            CloudQueueMessage thirdMessage = sortedMessages.ElementAt(2);
            AssertEqual(new PocoMessage { Value = "10" }, firstMessage);
            AssertEqual(new PocoMessage { Value = "20" }, secondMessage);
            AssertEqual(new PocoMessage { Value = "30" }, thirdMessage);
        }

        [Fact]
        public void Queue_IfBoundToIAsyncCollector_AddEnqueuesImmediately()
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();

            // Act
            Call(account, typeof(QueueProgram), "BindToIAsyncCollectorEnqueuesImmediately");
        }

        [Fact]
        public void Queue_IfBoundToCloudQueue_CanCall()
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();

            // Act
            CloudQueue result = Call<CloudQueue>(account, typeof(BindToCloudQueueProgram), "BindToCloudQueue",
                (s) => BindToCloudQueueProgram.TaskSource = s);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(QueueName, result.Name);
        }

        [Fact]
        public async Task Queue_IfBoundToCloudQueueAndQueueIsMissing_Creates()
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();

            // Act
            CloudQueue result = Call<CloudQueue>(account, typeof(BindToCloudQueueProgram), "BindToCloudQueue",
                (s) => BindToCloudQueueProgram.TaskSource = s);

            // Assert
            Assert.NotNull(result);
            CloudQueue queue = account.CreateCloudQueueClient().GetQueueReference(QueueName);
            Assert.True(await queue.ExistsAsync());
        }

        private class BindToCloudQueueProgram
        {
            public static TaskCompletionSource<CloudQueue> TaskSource { get; set; }

            public static void BindToCloudQueue([Queue(QueueName)] CloudQueue queue)
            {
                TaskSource.TrySetResult(queue);
            }
        }

        [Theory]
        [InlineData("FuncWithOutCloudQueueMessage", TestQueueMessage)]
        [InlineData("FuncWithOutByteArray", TestQueueMessage)]
        [InlineData("FuncWithOutString", TestQueueMessage)]
        [InlineData("FuncWithICollector", TestQueueMessage)]
        public async Task Queue_IfBoundToTypeAndQueueIsMissing_CreatesAndSends(string methodName, string expectedMessage)
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();

            // Act
            Call(account, typeof(MissingQueueProgram), methodName);

            // Assert
            CloudQueue queue = account.CreateCloudQueueClient().GetQueueReference(OutputQueueName);
            Assert.True(await queue.ExistsAsync());
            AssertMessageSent(expectedMessage, queue);
        }

        [Fact]
        public async Task Queue_IfBoundToOutPocoAndQueueIsMissing_CreatesAndSends()
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();

            // Act
            Call(account, typeof(MissingQueueProgram), "FuncWithOutT");

            // Assert
            CloudQueue queue = account.CreateCloudQueueClient().GetQueueReference(OutputQueueName);
            Assert.True(await queue.ExistsAsync());
            AssertMessageSent(new PocoMessage { Value = TestQueueMessage }, queue);
        }

        [Fact]
        public async Task Queue_IfBoundToOutStructAndQueueIsMissing_CreatesAndSends()
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();

            // Act
            Call(account, typeof(MissingQueueProgram), "FuncWithOutT");

            // Assert
            CloudQueue queue = account.CreateCloudQueueClient().GetQueueReference(OutputQueueName);
            Assert.True(await queue.ExistsAsync());
            AssertMessageSent(new StructMessage { Value = TestQueueMessage }, queue);
        }

        [Theory]
        [InlineData("FuncWithOutCloudQueueMessageNull")]
        [InlineData("FuncWithOutByteArrayNull")]
        [InlineData("FuncWithOutStringNull")]
        [InlineData("FuncWithICollectorNoop")]
        public async Task Queue_IfBoundToTypeAndQueueIsMissing_DoesNotCreate(string methodName)
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();

            // Act
            Call(account, typeof(MissingQueueProgram), methodName);

            // Assert
            CloudQueue queue = account.CreateCloudQueueClient().GetQueueReference(OutputQueueName);
            Assert.False(await queue.ExistsAsync());
        }

        [Fact]
        public void Binder_IfBindingBlobToTextWriter_CanCall()
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();

            // Act
            Call(account, typeof(BindToBinderBlobTextWriterProgram), "Call");

            // Assert
            var container = account.CreateCloudBlobClient().GetContainerReference(ContainerName);
            CloudBlockBlob blob = container.GetBlockBlobReference(OutputBlobName);
            string content = blob.DownloadText();
            Assert.Equal("output", content);
        }

        private class BindToBinderBlobTextWriterProgram
        {
            [NoAutomaticTrigger]
            public static void Call(IBinder binder)
            {
                TextWriter tw = binder.Bind<TextWriter>(new BlobAttribute(OutputBlobPath));
                tw.Write("output");

                // closed automatically 
            }
        }

        private static void AssertMessageSent(string expectedMessage, CloudQueue queue)
        {
            Assert.NotNull(queue);
            CloudQueueMessage message = queue.GetMessageAsync().GetAwaiter().GetResult();
            Assert.NotNull(message);
            Assert.Equal(expectedMessage, message.AsString);
        }

        private static void AssertMessageSent(PocoMessage expected, CloudQueue queue)
        {
            Assert.NotNull(queue);
            CloudQueueMessage message = queue.GetMessageAsync().GetAwaiter().GetResult();
            Assert.NotNull(message);
            AssertEqual(expected, message);
        }

        private static void AssertMessageSent(StructMessage expected, CloudQueue queue)
        {
            Assert.NotNull(queue);
            CloudQueueMessage message = queue.GetMessageAsync().GetAwaiter().GetResult();
            Assert.NotNull(message);
            AssertEqual(expected, message);
        }

        private static void AssertEqual(PocoMessage expected, CloudQueueMessage actualMessage)
        {
            Assert.NotNull(actualMessage);
            string content = actualMessage.AsString;
            PocoMessage actual = JsonConvert.DeserializeObject<PocoMessage>(content);
            AssertEqual(expected, actual);
        }

        private static void AssertEqual(StructMessage expected, CloudQueueMessage actualMessage)
        {
            Assert.NotNull(actualMessage);
            string content = actualMessage.AsString;
            StructMessage actual = JsonConvert.DeserializeObject<StructMessage>(content);
            AssertEqual(expected, actual);
        }

        private static void AssertEqual(PocoMessage expected, PocoMessage actual)
        {
            if (expected == null)
            {
                Assert.Null(actual);
                return;
            }

            Assert.Equal(expected.Value, actual.Value);
        }

        private static void AssertEqual(StructMessage expected, StructMessage actual)
        {
            Assert.Equal(expected.Value, actual.Value);
        }

        [Fact]
        public async Task BlobTrigger_IfCopiedViaPoco_CanCall()
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(ContainerName);
            CloudBlockBlob inputBlob = container.GetBlockBlobReference(BlobName);
            await container.CreateIfNotExistsAsync();
            await inputBlob.UploadTextAsync("abc");

            Dictionary<string, object> arguments = new Dictionary<string, object>
            {
                { "input", BlobPath }
            };

            // Act
            Call(account, typeof(CopyBlobViaPocoProgram), "CopyViaPoco", arguments, typeof(PocoBlobBinder));

            // Assert
            CloudBlockBlob outputBlob = container.GetBlockBlobReference(OutputBlobName);
            string content = outputBlob.DownloadText();
            Assert.Equal("*abc*", content);
        }

        private class CopyBlobViaPocoProgram
        {
            public static void CopyViaPoco(
                [BlobTrigger(BlobPath)] PocoBlob input,
                [Blob(OutputBlobPath)] out PocoBlob output)
            {
                output = new PocoBlob { Value = "*" + input.Value + "*" };
            }
        }

        private class PocoBlob
        {
            public string Value;
        }

        private class PocoBlobBinder : ICloudBlobStreamBinder<PocoBlob>
        {
            public async Task<PocoBlob> ReadFromStreamAsync(Stream input, CancellationToken cancellationToken)
            {
                TextReader reader = new StreamReader(input);
                string text = await reader.ReadToEndAsync();
                return new PocoBlob { Value = text };
            }

            public async Task WriteToStreamAsync(PocoBlob value, Stream output, CancellationToken cancellationToken)
            {
                TextWriter writer = new StreamWriter(output);
                await writer.WriteAsync(value.Value);
                await writer.FlushAsync();
            }
        }

        [Theory]
        [InlineData("FuncWithITableEntity")]
        [InlineData("FuncWithPocoObjectEntity")]
        [InlineData("FuncWithPocoValueEntity")]
        [InlineData("FuncWithICollector")]
        public async Task Table_IfBoundToTypeAndTableIsMissing_DoesNotCreate(string methodName)
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();
            CloudTableClient client = account.CreateCloudTableClient();
            CloudTable table = client.GetTableReference(TableName);

            // Act
            Call(account, typeof(MissingTableProgram), methodName);

            // Assert
            Assert.False(await table.ExistsAsync());
        }

        [Fact]
        public async Task Table_IfBoundToCloudTableAndTableIsMissing_Creates()
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();

            // Act
            CloudTable result = Call<CloudTable>(account, typeof(BindToCloudTableProgram), "BindToCloudTable",
                (s) => BindToCloudTableProgram.TaskSource = s);

            // Assert
            Assert.NotNull(result);
            var table = account.CreateCloudTableClient().GetTableReference(TableName);
            Assert.True(await table.ExistsAsync());
        }

        private class BindToCloudTableProgram
        {
            public static TaskCompletionSource<CloudTable> TaskSource { get; set; }

            public static void BindToCloudTable([Table(TableName)] CloudTable queue)
            {
                TaskSource.TrySetResult(queue);
            }
        }

        [Fact]
        public void Table_IfBoundToICollectorITableEntity_CanCall()
        {
            TestTableBoundToCollectorCanCall(typeof(BindTableToICollectorITableEntity));
        }

        private class BindTableToICollectorITableEntity
        {
            public static void Call([Table(TableName)] ICollector<ITableEntity> table)
            {
                table.Add(new DynamicTableEntity(PartitionKey, RowKey));
            }
        }

        [Fact]
        public void Table_IfBoundToICollectorDynamicTableEntity_CanCall()
        {
            TestTableBoundToCollectorCanCall(typeof(BindTableToICollectorDynamicTableEntity));
        }

        private class BindTableToICollectorDynamicTableEntity
        {
            public static void Call([Table(TableName)] ICollector<DynamicTableEntity> table)
            {
                table.Add(new DynamicTableEntity(PartitionKey, RowKey));
            }
        }

        [Fact]
        public void Table_IfBoundToICollectorSdkTableEntity_CanCall()
        {
            TestTableBoundToCollectorCanCall(typeof(BindTableToICollectorSdkTableEntity));
        }

        private class BindTableToICollectorSdkTableEntity
        {
            public static void Call([Table(TableName)] ICollector<SdkTableEntity> table)
            {
                table.Add(new SdkTableEntity { PartitionKey = PartitionKey, RowKey = RowKey });
            }
        }

        [Fact]
        public void Table_IfBoundToIAsyncCollectorITableEntity_CanCall()
        {
            TestTableBoundToCollectorCanCall(typeof(BindTableToIAsyncCollectorITableEntity));
        }

        private class BindTableToIAsyncCollectorITableEntity
        {
            public static Task Call([Table(TableName)] IAsyncCollector<ITableEntity> table)
            {
                return table.AddAsync(new DynamicTableEntity(PartitionKey, RowKey));
            }
        }

        [Fact]
        public void Table_IfBoundToIAsyncCollectorDynamicTableEntity_CanCall()
        {
            TestTableBoundToCollectorCanCall(typeof(BindTableToIAsyncCollectorDynamicTableEntity));
        }

        private class BindTableToIAsyncCollectorDynamicTableEntity
        {
            public static Task Call([Table(TableName)] IAsyncCollector<DynamicTableEntity> table)
            {
                return table.AddAsync(new DynamicTableEntity(PartitionKey, RowKey));
            }
        }

        [Fact]
        public void Table_IfBoundToIAsyncCollectorSdkTableEntity_CanCall()
        {
            TestTableBoundToCollectorCanCall(typeof(BindTableToIAsyncCollectorSdkTableEntity));
        }

        private class BindTableToIAsyncCollectorSdkTableEntity
        {
            public static Task Call([Table(TableName)] IAsyncCollector<SdkTableEntity> table)
            {
                return table.AddAsync(new SdkTableEntity { PartitionKey = PartitionKey, RowKey = RowKey });
            }
        }

        private static void TestTableBoundToCollectorCanCall(Type programType)
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();

            // Act
            Call(account, programType, "Call");

            // Assert
            CloudTableClient client = account.CreateCloudTableClient();
            CloudTable table = client.GetTableReference(TableName);
            DynamicTableEntity entity = table.Retrieve<DynamicTableEntity>(PartitionKey, RowKey);
            Assert.NotNull(entity);
        }

        [Fact]
        public void Table_IfBoundToCollectorAndETagDoesNotMatch_Throws()
        {
            TestBindToConcurrentlyUpdatedTableEntity(typeof(BindTableToCollectorFoo), "collector");
        }

        private class BindTableToCollectorFoo
        {
            public static void Call([Table(TableName)] ICollector<ITableEntity> collector,
                [Table(TableName)] CloudTable table)
            {
                SdkTableEntity entity = table.Retrieve<SdkTableEntity>(PartitionKey, RowKey);
                Assert.NotNull(entity);
                Assert.Equal("Foo", entity.Value);

                // Update the entity to invalidate the version read by this method.
                table.Replace(new SdkTableEntity
                {
                    PartitionKey = PartitionKey,
                    RowKey = RowKey,
                    ETag = "*",
                    Value = "FooBackground"
                });

                // The attempted update by this method should now fail.
                collector.Add(new DynamicTableEntity(PartitionKey, RowKey, entity.ETag,
                    new Dictionary<string, EntityProperty> { { "Value", new EntityProperty("Bar") } }));
            }
        }

        private static XStorageAccount GetRealStorage()
        {
            // Arrange            
            
            var acs = Environment.GetEnvironmentVariable("AzureWebJobsDashboard");
            var account = XStorageAccount.NewFromConnectionString(acs);
            return account;
        }

        [Fact]
        [Trait("SecretsRequired", "true")]
        public async Task TableEntity_IfBoundToJArray_CanCall()
        {
            XStorageAccount account = GetRealStorage(); // Fake storage doesn't implement table filters

            var client = account.CreateCloudTableClient();
            CloudTable table = client.GetTableReference(TableName);
            await table.CreateIfNotExistsAsync();
            table.InsertOrReplace(CreateTableEntity(PartitionKey, RowKey + "1", "Value", "x1", "*"));
            table.InsertOrReplace(CreateTableEntity(PartitionKey, RowKey + "2", "Value", "x2", "*"));
            table.InsertOrReplace(CreateTableEntity(PartitionKey, RowKey + "3", "Value", "x3", "*"));
            table.InsertOrReplace(CreateTableEntity(PartitionKey, RowKey + "4", "Value", "x4", "*"));

            var instance = new BindTableEntityToJArrayProgram();
            var jobActivator = new FakeActivator();
            jobActivator.Add(instance);

            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<BindTableEntityToJArrayProgram>()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IJobActivator>(jobActivator);
                    services.AddSingleton<XStorageAccountProvider>(new FakeStorageAccountProvider(account));
                })
                .AddStorageBindings()
                .Build();

            // Act
            Type type = typeof(BindTableEntityToJArrayProgram);
            host.GetJobHost().Call(type.GetMethod(nameof(BindTableEntityToJArrayProgram.CallTakeFilter)));
            Assert.Equal("x1;x3;", instance._result);

            host.GetJobHost().Call(type.GetMethod(nameof(BindTableEntityToJArrayProgram.CallFilter)));
            Assert.Equal("x1;x3;x4;", instance._result);

            host.GetJobHost().Call(type.GetMethod(nameof(BindTableEntityToJArrayProgram.CallTake)));
            Assert.Equal("x1;x2;x3;", instance._result);

            host.GetJobHost().Call(type.GetMethod(nameof(BindTableEntityToJArrayProgram.Call)));
            Assert.Equal("x1;x2;x3;x4;", instance._result);
        }

        private class BindTableEntityToJArrayProgram
        {
            public string _result;

            // Helper to flatten a Jarray for quick testing. 
            static string Flatten(JArray array)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < array.Count; i++)
                {
                    sb.Append(array[i]["Value"]);
                    sb.Append(';');
                }
                return sb.ToString();
            }

            public void CallTakeFilter([Table(TableName, PartitionKey, Take = 2, Filter = "Value ne 'x2'")] JArray array)
            {
                this._result = Flatten(array);
            }

            public void CallFilter([Table(TableName, PartitionKey, Filter = "Value ne 'x2'")] JArray array)
            {
                this._result = Flatten(array);
            }

            public void CallTake([Table(TableName, PartitionKey, Take = 3)] JArray array)
            {
                this._result = Flatten(array);
            }

            // No take or filters
            public void Call([Table(TableName, PartitionKey)] JArray array)
            {
                this._result = Flatten(array);
            }
        }

        [Fact]
        public async Task TableEntity_IfBoundToJObject_CanCall()
        {
            // Arrange
            var account = CreateFakeStorageAccount();
            var client = account.CreateCloudTableClient();
            var table = client.GetTableReference(TableName);
            await table.CreateIfNotExistsAsync();
            table.Insert(CreateTableEntity(PartitionKey, RowKey, "Value", "Foo"));

            // Act
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost<BindTableEntityToJObjectProgram>()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<XStorageAccountProvider>(new FakeStorageAccountProvider(account));
                })
                .AddStorageBindings()
                .Build();

            var prog = host.GetJobHost<BindTableEntityToJObjectProgram>();

            prog.Call("Call", new
            {
                table = TableName, // Test resolution 
                pk1 = PartitionKey,
                rk1 = RowKey
            });

            // Assert
            SdkTableEntity entity = table.Retrieve<SdkTableEntity>(PartitionKey, RowKey);
            Assert.NotNull(entity);
        }

        private class BindTableEntityToJObjectProgram
        {
            public static void Call([Table("{table}", "{pk1}", "{rk1}")] JObject entity)
            {
                Assert.NotNull(entity);
                Assert.Equal("Foo", entity["Value"].ToString());
            }
        }

        [Fact]
        public async Task TableEntity_IfBoundToSdkTableEntity_CanCall()
        {
            // Arrange
            var account = CreateFakeStorageAccount();
            var client = account.CreateCloudTableClient();
            var table = client.GetTableReference(TableName);
            await table.CreateIfNotExistsAsync();
            table.Insert(CreateTableEntity(PartitionKey, RowKey, "Value", "Foo"));

            // Act
            Call(account, typeof(BindTableEntityToSdkTableEntityProgram), "Call");

            // Assert
            SdkTableEntity entity = table.Retrieve<SdkTableEntity>(PartitionKey, RowKey);
            Assert.NotNull(entity);
            Assert.Equal("Bar", entity.Value);
        }

        private class BindTableEntityToSdkTableEntityProgram
        {
            public static void Call([Table(TableName, PartitionKey, RowKey)] SdkTableEntity entity)
            {
                Assert.NotNull(entity);
                Assert.Equal("Foo", entity.Value);
                entity.Value = "Bar";
            }
        }

        [Fact]
        public async Task TableEntity_IfBoundToPocoTableEntity_CanCall()
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();
            var client = account.CreateCloudTableClient();
            var table = client.GetTableReference(TableName);
            await table.CreateIfNotExistsAsync();
            table.Insert(new DynamicTableEntity(PartitionKey, RowKey, null, new Dictionary<string, EntityProperty>
            {
                { "Fruit", new EntityProperty("Banana") },
                { "Duration", new EntityProperty("\"00:00:01\"") },
                { "Value", new EntityProperty("Foo") }
            }));

            // Act
            Call(account, typeof(BindTableEntityToPocoTableEntityProgram), "Call");

            // Assert
            DynamicTableEntity entity = table.Retrieve<DynamicTableEntity>(PartitionKey, RowKey);
            Assert.NotNull(entity);
            Assert.Equal(PartitionKey, entity.PartitionKey); // Guard
            Assert.Equal(RowKey, entity.RowKey); // Guard
            IDictionary<string, EntityProperty> properties = entity.Properties;
            Assert.Equal(3, properties.Count);
            Assert.True(properties.ContainsKey("Value"));
            EntityProperty fruitProperty = properties["Fruit"];
            Assert.Equal(EdmType.String, fruitProperty.PropertyType);
            Assert.Equal("Pear", fruitProperty.StringValue);
            EntityProperty durationProperty = properties["Duration"];
            Assert.Equal(EdmType.String, durationProperty.PropertyType);
            Assert.Equal("\"00:02:00\"", durationProperty.StringValue);
            EntityProperty valueProperty = properties["Value"];
            Assert.Equal(EdmType.String, valueProperty.PropertyType);
            Assert.Equal("Bar", valueProperty.StringValue);
        }

        private class BindTableEntityToPocoTableEntityProgram
        {
            public static void Call([Table(TableName, PartitionKey, RowKey)] PocoTableEntityWithEnum entity)
            {
                Assert.NotNull(entity);
                Assert.Equal(Fruit.Banana, entity.Fruit);
                Assert.Equal(TimeSpan.FromSeconds(1), entity.Duration);
                Assert.Equal("Foo", entity.Value);

                entity.Fruit = Fruit.Pear;
                entity.Duration = TimeSpan.FromMinutes(2);
                entity.Value = "Bar";
            }
        }

        private class PocoTableEntityWithEnum
        {
            public Fruit Fruit { get; set; }
            public TimeSpan Duration { get; set; }
            public string Value { get; set; }
        }

        private enum Fruit
        {
            Apple,
            Banana,
            Pear
        }

        [Fact]
        public void TableEntity_IfBoundToSdkTableEntityAndUpdatedConcurrently_Throws()
        {
            TestBindTableEntityToConcurrentlyUpdatedValue(typeof(BindTableEntityToConcurrentlyUpdatedSdkTableEntity));
        }

        private class BindTableEntityToConcurrentlyUpdatedSdkTableEntity
        {
            public static void Call([Table(TableName, PartitionKey, RowKey)] SdkTableEntity entity,
                [Table(TableName)]CloudTable table)
            {
                Assert.NotNull(entity);
                Assert.Equal("Foo", entity.Value);

                // Update the entity to invalidate the version read by this method.
                table.Replace(new SdkTableEntity
                {
                    PartitionKey = PartitionKey,
                    RowKey = RowKey,
                    ETag = "*",
                    Value = "FooBackground"
                });

                // The attempted update by this method should now fail.
                entity.Value = "Bar";
            }
        }

        [Fact]
        public async Task TableEntity_IfBoundToPocoTableEntityAndUpdatedConcurrently_Throws()
        {
            await TestBindTableEntityToConcurrentlyUpdatedValue(typeof(BindTableEntityToConcurrentlyUpdatedPocoTableEntity));
        }

        private class BindTableEntityToConcurrentlyUpdatedPocoTableEntity
        {
            public static void Call([Table(TableName, PartitionKey, RowKey)] PocoTableEntity entity,
                [Table(TableName)]CloudTable table)
            {
                Assert.NotNull(entity);
                Assert.Equal("Foo", entity.Value);

                // Update the entity to invalidate the version read by this method.
                table.Replace(new SdkTableEntity
                {
                    PartitionKey = PartitionKey,
                    RowKey = RowKey,
                    ETag = "*",
                    Value = "FooBackground"
                });

                // The attempted update by this method should now fail.
                entity.Value = "Bar";
            }
        }

        private static async Task TestBindTableEntityToConcurrentlyUpdatedValue(Type programType)
        {
            await TestBindToConcurrentlyUpdatedTableEntity(programType, "entity");
        }

        private static async Task TestBindToConcurrentlyUpdatedTableEntity(Type programType, string parameterName)
        {
            // Arrange
            XStorageAccount account = CreateFakeStorageAccount();
            var client = account.CreateCloudTableClient();
            CloudTable table = client.GetTableReference(TableName);
            await table.CreateIfNotExistsAsync();
            table.Insert(CreateTableEntity(PartitionKey, RowKey, "Value", "Foo"));

            // Act & Assert
            Exception exception = CallFailure(account, programType, "Call");
            AssertInvocationETagFailure(parameterName, exception);

            SdkTableEntity entity = table.Retrieve<SdkTableEntity>(PartitionKey, RowKey);
            Assert.NotNull(entity);
            Assert.Equal("FooBackground", entity.Value);
        }

        private static void AssertInvocationETagFailure(string expectedParameterName, Exception exception)
        {
            Assert.IsType<FunctionInvocationException>(exception);
            Assert.IsType<InvalidOperationException>(exception.InnerException);
            string expectedMessage = String.Format(CultureInfo.InvariantCulture,
                "Error while handling parameter {0} after function returned:", expectedParameterName);
            Assert.Equal(expectedMessage, exception.InnerException.Message);
            Exception innerException = exception.InnerException.InnerException;
            Assert.IsType<InvalidOperationException>(innerException);
            // This exception is an implementation detail of the fake storage account. A real one would use a
            // StorageException (this assert may need to change if the fake is updated to be more realistic).
            InvalidOperationException invalidOperationException = (InvalidOperationException)innerException;
            Assert.NotNull(invalidOperationException.Message);
            Assert.True(invalidOperationException.Message.StartsWith("Entity PK='PK',RK='RK' does not match eTag"));
        }

        private static void Call(XStorageAccount account, Type programType, string methodName,
            params Type[] cloudBlobStreamBinderTypes)
        {
            FunctionalTest.Call(account, programType, programType.GetMethod(methodName), arguments: null,
                cloudBlobStreamBinderTypes: cloudBlobStreamBinderTypes);
        }

        private static void Call(XStorageAccount account, Type programType, string methodName,
            IDictionary<string, object> arguments, params Type[] cloudBlobStreamBinderTypes)
        {
            FunctionalTest.Call(account, programType, programType.GetMethod(methodName), arguments,
                cloudBlobStreamBinderTypes);
        }

        private static TResult Call<TResult>(XStorageAccount account, Type programType, string methodName,
            Action<TaskCompletionSource<TResult>> setTaskSource)
        {
            IDictionary<string, object> arguments = null;
            return FunctionalTest.Call<TResult>(account, programType, programType.GetMethod(methodName), arguments,
                setTaskSource);
        }

        private static TResult Call<TResult>(XStorageAccount account, Type programType, string methodName,
            IDictionary<string, object> arguments, Action<TaskCompletionSource<TResult>> setTaskSource)
        {
            return FunctionalTest.Call<TResult>(account, programType, programType.GetMethod(methodName), arguments,
                setTaskSource);
        }

        private static Exception CallFailure(XStorageAccount account, Type programType, string methodName)
        {
            return FunctionalTest.CallFailure(account, programType, programType.GetMethod(methodName), null);
        }

        private static XStorageAccount CreateFakeStorageAccount()
        {
            return new XFakeStorageAccount(); 
        }

        private static ITableEntity CreateTableEntity(string partitionKey, string rowKey, string propertyName,
            string propertyValue, string eTag = null)
        {
            return new DynamicTableEntity(partitionKey, rowKey, eTag, new Dictionary<string, EntityProperty>
            {
                { propertyName, new EntityProperty(propertyValue) }
            });
        }

        private struct CustomDataValue
        {
            public int ValueId { get; set; }
            public string Content { get; set; }
        }

        private class CustomDataObject
        {
            public int ValueId { get; set; }
            public string Content { get; set; }
        }

        private class MissingBlobToCustomObjectBinder : ICloudBlobStreamBinder<CustomDataObject>
        {
            public Task<CustomDataObject> ReadFromStreamAsync(Stream input, CancellationToken cancellationToken)
            {
                // Read() shouldn't be called if the stream is missing. 
                Assert.False(true, "If stream is missing, should never call Read() converter");

                return Task.FromResult<CustomDataObject>(null);
            }

            public Task WriteToStreamAsync(CustomDataObject value, Stream output, CancellationToken cancellationToken)
            {
                Assert.NotNull(output);

                if (value != null)
                {
                    Assert.Equal(TestValue, value.ValueId);

                    const byte ignore = 0xFF;
                    output.WriteByte(ignore);
                }

                return Task.FromResult(0);
            }
        }

        private class MissingBlobToCustomValueBinder : ICloudBlobStreamBinder<CustomDataValue>
        {
            public Task<CustomDataValue> ReadFromStreamAsync(Stream input, CancellationToken cancellationToken)
            {
                // Read() shouldn't be called if the stream is missing. 
                Assert.False(true, "If stream is missing, should never call Read() converter");

                return Task.FromResult<CustomDataValue>(new CustomDataValue());
            }

            public Task WriteToStreamAsync(CustomDataValue value, Stream output, CancellationToken cancellationToken)
            {
                Assert.NotNull(output);

                Assert.Equal(TestValue, value.ValueId);

                const byte ignore = 0xFF;
                output.WriteByte(ignore);

                return Task.FromResult(0);
            }
        }

        private class MissingBlobProgram
        {
            public static void FuncWithBlockBlob([Blob(BlobPath)] CloudBlockBlob blob)
            {
                Assert.NotNull(blob);
                Assert.Equal(BlobName, blob.Name);
                Assert.Equal(ContainerName, blob.Container.Name);
            }

            public static void FuncWithStreamRead([Blob(BlobPath, FileAccess.Read)] Stream stream)
            {
                Assert.Null(stream);
            }

            public static void FuncWithStreamWrite([Blob(BlobPath, FileAccess.Write)] Stream stream)
            {
                Assert.NotNull(stream);

                const byte ignore = 0xFF;
                stream.WriteByte(ignore);
            }

            public static void FuncWithStreamWriteNoop([Blob(BlobPath, FileAccess.Write)] Stream stream)
            {
                Assert.NotNull(stream);
            }

            public static void FuncWithTextReader([Blob(BlobPath)] TextReader reader)
            {
                Assert.Null(reader);
            }

            public static void FuncWithTextWriter([Blob(BlobPath)] TextWriter writer)
            {
                Assert.NotNull(writer);
            }

            public static void FuncWithString([Blob(BlobPath)] string content)
            {
                Assert.Null(content);
            }

            public static void FuncWithOutString([Blob(BlobPath)] out string content)
            {
                content = "ignore";
            }

            public static void FuncWithOutStringNull([Blob(BlobPath)] out string content)
            {
                content = null;
            }

            public static void FuncWithT([Blob(BlobPath)] CustomDataObject value)
            {
                Assert.Null(value); // null value is Blob is Missing 
            }

            public static void FuncWithOutT([Blob(BlobPath)] out CustomDataObject value)
            {
                value = new CustomDataObject { ValueId = TestValue, Content = "ignore" };
            }

            public static void FuncWithOutTNull([Blob(BlobPath)] out CustomDataObject value)
            {
                value = null;
            }

            public static void FuncWithValueT([Blob(BlobPath)] CustomDataValue value)
            {
                // default(T) is blob is missing 
                Assert.NotNull(value);
                Assert.Equal(0, value.ValueId);
            }

            public static void FuncWithOutValueT([Blob(BlobPath)] out CustomDataValue value)
            {
                value = new CustomDataValue { ValueId = TestValue, Content = "ignore" };
            }
        }

        private class BlobProgram
        {
            // This can be invoked explicitly (and providing parameters)
            // or it can be invoked implicitly by triggering on input. // (assuming no unbound parameters)
            [NoAutomaticTrigger]
            public static void UnboundParameter(
                string name, string date,  // used by input
                string unbound, // not used by in/out
                [BlobTrigger(ContainerName + "/{name}-{date}.csv")] TextReader values,
                [Blob(ContainerName + "/{name}.csv")] TextWriter output
                )
            {
                Assert.Equal("test", unbound);
                Assert.Equal("note", name);
                Assert.Equal("monday", date);

                string content = values.ReadToEnd();
                Assert.Equal("abc", content);

                output.Write("done");
            }

            public static void BindToCloudBlockBlob([Blob(BlobPath)] CloudBlockBlob blob)
            {
                Assert.NotNull(blob);
                Assert.Equal(BlobName, blob.Name);
            }

            public static void BindToString([Blob(BlobPath)] string content)
            {
                Assert.NotNull(content);
                string[] strings = content.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                // Verify expected number of entries in CloudBlob
                Assert.Equal(3, strings.Length);
                for (int i = 0; i < 3; ++i)
                {
                    int value;
                    bool parsed = int.TryParse(strings[i], out value);
                    string message = String.Format("Unable to parse CloudBlob strings[{0}]: '{1}'", i, strings[i]);
                    Assert.True(parsed, message);
                    // Ensure expected value in CloudBlob
                    Assert.Equal(i, value);
                }
            }

            public static void CopyViaString(
                [Blob(BlobPath)] string blobIn,
                [Blob(OutputBlobPath)] out string blobOut
                )
            {
                blobOut = blobIn;
            }

            public static void CopyViaTextReaderTextWriter(
                [BlobTrigger(BlobPath)] TextReader values,
                [Blob(OutputBlobPath)] TextWriter output)
            {
                string content = values.ReadToEnd();
                output.Write(content);
            }
        }

        private class BlobTriggerBindToICloudBlobProgram
        {
            public static TaskCompletionSource<ICloudBlob> TaskSource { get; set; }

            public static void Call([BlobTrigger(BlobPath)] ICloudBlob blob)
            {
                TaskSource.TrySetResult(blob);
            }
        }

        private class QueueNotSupportedProgram
        {
            public static void BindToICollectorInt(
                [Queue(OutputQueueName)] ICollector<int> output)
            {
                // not supported
            }
        }

        private class QueueProgram
        {
            public static void BindToOutPoco([Queue(OutputQueueName)] out PocoMessage output)
            {
                output = new PocoMessage { Value = "15" };
            }

            public static void BindToICollectorPoco([Queue(OutputQueueName)] ICollector<PocoMessage> output)
            {
                output.Add(new PocoMessage { Value = "10" });
                output.Add(new PocoMessage { Value = "20" });
                output.Add(new PocoMessage { Value = "30" });
            }

            public static async Task BindToIAsyncCollectorPoco(
                [Queue(OutputQueueName)] IAsyncCollector<PocoMessage> output)
            {
                await output.AddAsync(new PocoMessage { Value = "10" });
                await output.AddAsync(new PocoMessage { Value = "20" });
                await output.AddAsync(new PocoMessage { Value = "30" });
            }

            public static async Task BindToIAsyncCollectorByteArray(
                [Queue(OutputQueueName)] IAsyncCollector<byte[]> output)
            {
                await output.AddAsync(Encoding.UTF8.GetBytes("test1"));
                await output.AddAsync(Encoding.UTF8.GetBytes("test2"));
                await output.AddAsync(Encoding.UTF8.GetBytes("test3"));
            }

            public static void BindToICollectorByteArray(
                [Queue(OutputQueueName)] ICollector<byte[]> output)
            {
                output.Add(Encoding.UTF8.GetBytes("test1"));
                output.Add(Encoding.UTF8.GetBytes("test2"));
                output.Add(Encoding.UTF8.GetBytes("test3"));
            }

            public static async Task BindToIAsyncCollectorEnqueuesImmediately(
                [Queue(OutputQueueName)] IAsyncCollector<string> collector,
                [Queue(OutputQueueName)] CloudQueue queue)
            {
                string expectedContents = "Enqueued immediately";
                await collector.AddAsync(expectedContents);
                CloudQueueMessage message = await queue.GetMessageAsync();
                Assert.NotNull(message);
                Assert.Equal(expectedContents, message.AsString);
            }
        }

        private class PocoMessage
        {
            public string Value { get; set; }
        }

        private struct StructMessage
        {
            public string Value { get; set; }
        }

        private class MissingQueueProgram
        {
            public static void FuncWithOutCloudQueueMessage([Queue(OutputQueueName)] out CloudQueueMessage message)
            {
                message = new CloudQueueMessage(TestQueueMessage);
            }

            public static void FuncWithOutCloudQueueMessageNull([Queue(OutputQueueName)] out CloudQueueMessage message)
            {
                message = null;
            }

            public static void FuncWithOutByteArray([Queue(OutputQueueName)] out byte[] payload)
            {
                payload = Encoding.UTF8.GetBytes(TestQueueMessage);
            }

            public static void FuncWithOutByteArrayNull([Queue(OutputQueueName)] out byte[] payload)
            {
                payload = null;
            }

            public static void FuncWithOutString([Queue(OutputQueueName)] out string payload)
            {
                payload = TestQueueMessage;
            }

            public static void FuncWithOutStringNull([Queue(OutputQueueName)] out string payload)
            {
                payload = null;
            }

            public static void FuncWithICollector([Queue(OutputQueueName)] ICollector<string> queue)
            {
                Assert.NotNull(queue);
                queue.Add(TestQueueMessage);
            }

            public static void FuncWithICollectorNoop([Queue(QueueName)] ICollector<PocoMessage> queue)
            {
                Assert.NotNull(queue);
            }

            public static void FuncWithOutT([Queue(OutputQueueName)] out PocoMessage value)
            {
                value = new PocoMessage { Value = TestQueueMessage };
            }

            public static void FuncWithOutTNull([Queue(OutputQueueName)] out PocoMessage value)
            {
                value = null;
            }

            public static void FuncWithOutValueT([Queue(OutputQueueName)] out StructMessage value)
            {
                value = new StructMessage { Value = TestQueueMessage };
            }
        }

        private class MissingTableProgram
        {
            public static void FuncWithICollector([Table(TableName)] ICollector<SdkTableEntity> entities)
            {
                Assert.NotNull(entities);
            }

            public static void FuncWithITableEntity([Table(TableName, "PK", "RK")] SdkTableEntity entity)
            {
                Assert.Null(entity);
            }

            public static void FuncWithPocoObjectEntity([Table(TableName, "PK", "RK")] PocoTableEntity entity)
            {
                Assert.Null(entity);
            }

            public static void FuncWithPocoValueEntity([Table(TableName, "PK", "RK")] StructTableEntity entity)
            {
                Assert.Null(entity.Value);
            }
        }

        private class SdkTableEntity : TableEntity
        {
            public string Value { get; set; }
        }

        private class PocoTableEntity
        {
            public string Value { get; set; }
        }

        private struct StructTableEntity
        {
            public string Value { get; set; }
        }
    }
}
