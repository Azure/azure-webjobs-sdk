// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Queue;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests
{
    public class BinderTests
    {
        private const string QueueName = "input";

        [Fact]
        public async Task Trigger_ViaIBinder_CannotBind()
        {
            // Arrange
            const string expectedContents = "abc";
            var account = CreateFakeStorageAccount();
            CloudQueue queue = CreateQueue(account, QueueName);
            CloudQueueMessage message = new CloudQueueMessage(expectedContents);
            await queue.AddMessageAsync(message);

            // Act
            Exception exception = RunTriggerFailure<string>(account, typeof(BindToQueueTriggerViaIBinderProgram),
                (s) => BindToQueueTriggerViaIBinderProgram.TaskSource = s);

            // Assert
            Assert.Equal("No binding found for attribute 'Microsoft.Azure.WebJobs.QueueTriggerAttribute'.", exception.Message);
        }

        private static XStorageAccount CreateFakeStorageAccount()
        {
            return new XFakeStorageAccount();
        }

        private static CloudQueue CreateQueue(XStorageAccount account, string queueName)
        {
            CloudQueueClient client = account.CreateCloudQueueClient();
            CloudQueue queue = client.GetQueueReference(queueName);
            queue.CreateIfNotExistsAsync().GetAwaiter().GetResult();
            return queue;
        }

        private static Exception RunTriggerFailure<TResult>(XStorageAccount account, Type programType,
            Action<TaskCompletionSource<TResult>> setTaskSource)
        {
            return FunctionalTest.RunTriggerFailure<TResult>(account, programType, setTaskSource);
        }

        private class BindToQueueTriggerViaIBinderProgram
        {
            public static TaskCompletionSource<string> TaskSource { get; set; }

            public static void Run([QueueTrigger(QueueName)] CloudQueueMessage message, IBinder binder)
            {
                TaskSource.TrySetResult(binder.Bind<string>(new QueueTriggerAttribute(QueueName)));
            }
        }
    }
}
