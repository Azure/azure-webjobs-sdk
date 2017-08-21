﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Queue;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Storage.IntegrationTests
{
    [Trait("SecretsRequired", "true")]
    public class StorageAccountTests
    {
        [Fact]
        public async Task CloudQueueCreate_IfNotExist_CreatesQueue()
        {
            // Arrange
            CloudStorageAccount sdkAccount = CreateSdkAccount();
            string queueName = GetQueueName("create-queue");

            CloudQueue sdkQueue = CreateSdkQueue(sdkAccount, queueName);

            try
            {
                IStorageAccount product = CreateProductUnderTest(sdkAccount);
                IStorageQueueClient client = product.CreateQueueClient();
                Assert.NotNull(client); // Guard
                IStorageQueue queue = client.GetQueueReference(queueName);
                Assert.NotNull(queue); // Guard

                // Act
                await queue.CreateIfNotExistsAsync(CancellationToken.None);

                // Assert
                Assert.True(await sdkQueue.ExistsAsync());
            }
            finally
            {
                if (await sdkQueue.ExistsAsync())
                {
                    await sdkQueue.DeleteAsync();
                }
            }
        }

        [Fact]
        public async Task CloudQueueAddMessage_AddsMessage()
        {
            // Arrange
            CloudStorageAccount sdkAccount = CreateSdkAccount();
            string queueName = GetQueueName("add-message");

            CloudQueue sdkQueue = CreateSdkQueue(sdkAccount, queueName);
            await sdkQueue.CreateIfNotExistsAsync();

            try
            {
                string expectedContent = "hello";

                IStorageAccount product = CreateProductUnderTest(sdkAccount);
                IStorageQueueClient client = product.CreateQueueClient();
                Assert.NotNull(client); // Guard
                IStorageQueue queue = client.GetQueueReference(queueName);
                Assert.NotNull(queue); // Guard

                IStorageQueueMessage message = queue.CreateMessage(expectedContent);
                Assert.NotNull(message); // Guard

                // Act
                queue.AddMessageAsync(message, CancellationToken.None).GetAwaiter().GetResult();

                // Assert
                CloudQueueMessage sdkMessage = await sdkQueue.GetMessageAsync();
                Assert.NotNull(sdkMessage);
                Assert.Equal(expectedContent, sdkMessage.AsString);
            }
            finally
            {
                await sdkQueue.DeleteAsync();
            }
        }

        private static IStorageAccount CreateProductUnderTest(CloudStorageAccount account)
        {
            Mock<IServiceProvider> servicesMock = new Mock<IServiceProvider>(MockBehavior.Strict);
            StorageClientFactory clientFactory = new StorageClientFactory();
            servicesMock.Setup(p => p.GetService(typeof(StorageClientFactory))).Returns(clientFactory);

            return new StorageAccount(account, servicesMock.Object);
        }

        private static CloudStorageAccount CreateSdkAccount()
        {
            return CloudStorageAccount.Parse(GetConnectionString());
        }

        private static CloudQueue CreateSdkQueue(CloudStorageAccount sdkAccount, string queueName)
        {
            CloudQueueClient sdkClient = sdkAccount.CreateCloudQueueClient();
            return sdkClient.GetQueueReference(queueName);
        }

        private static string GetConnectionString()
        {
            const string name = "AzureWebJobsStorage";
            string value = GetConnectionString(name);

            if (String.IsNullOrEmpty(value))
            {
                string message = String.Format(
                    "This test needs an Azure storage connection string to run. Please set the '{0}' environment " +
                    "variable or App.config connection string before running this test.", name);
                throw new InvalidOperationException(message);
            }

            return value;
        }

        private static string GetConnectionString(string connectionStringName)
            => ConfigurationUtility.GetConnectionString(connectionStringName);

        private static string GetQueueName(string infix)
        {
            return String.Format("test-{0}-{1:N}", infix, Guid.NewGuid());
        }
    }
}
