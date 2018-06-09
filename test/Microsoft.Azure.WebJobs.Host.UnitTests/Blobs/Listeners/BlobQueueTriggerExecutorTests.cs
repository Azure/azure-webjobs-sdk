﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Blobs;
using Microsoft.Azure.WebJobs.Host.Blobs.Listeners;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.Storage.Queue;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Blobs.Listeners
{
    public class BlobQueueTriggerExecutorTests
    {
        private const string TestBlobName = "TestBlobName";

        [Fact]
        public void ExecuteAsync_IfMessageIsNotJson_Throws()
        {
            // Arrange
            BlobQueueTriggerExecutor product = CreateProductUnderTest();
            IStorageQueueMessage message = CreateMessage("ThisIsNotValidJson");

            // Act
            Task task = product.ExecuteAsync(message, CancellationToken.None);

            // Assert
            Assert.Throws<JsonReaderException>(() => task.GetAwaiter().GetResult());
        }

        [Fact]
        public void ExecuteAsync_IfMessageIsJsonNull_Throws()
        {
            // Arrange
            BlobQueueTriggerExecutor product = CreateProductUnderTest();
            IStorageQueueMessage message = CreateMessage("null");

            // Act
            Task task = product.ExecuteAsync(message, CancellationToken.None);

            // Assert
            ExceptionAssert.ThrowsInvalidOperation(() => task.GetAwaiter().GetResult(),
                "Invalid blob trigger message.");
        }

        [Fact]
        public void ExecuteAsync_IfFunctionIdIsNull_Throws()
        {
            // Arrange
            BlobQueueTriggerExecutor product = CreateProductUnderTest();
            IStorageQueueMessage message = CreateMessage("{}");

            // Act
            Task task = product.ExecuteAsync(message, CancellationToken.None);

            // Assert
            ExceptionAssert.ThrowsInvalidOperation(() => task.GetAwaiter().GetResult(), "Invalid function ID.");
        }

        [Fact]
        public void ExecuteAsync_IfMessageIsFunctionIdIsNotRegistered_ReturnsSuccessResult()
        {
            // Arrange
            BlobQueueTriggerExecutor product = CreateProductUnderTest();
            IStorageQueueMessage message = CreateMessage(new BlobTriggerMessage { FunctionId = "Missing" });

            // Act
            Task<FunctionResult> task = product.ExecuteAsync(message, CancellationToken.None);

            // Assert
            Assert.True(task.Result.Succeeded);
        }

        [Theory]
        [InlineData(BlobType.BlockBlob)]
        [InlineData(BlobType.PageBlob)]
        public void ExecuteAsync_IfMessageIsFunctionIdIsRegistered_GetsETag(BlobType expectedBlobType)
        {
            // Arrange
            string expectedContainerName = "container";
            string expectedBlobName = TestBlobName;
            string functionId = "FunctionId";
            Mock<IBlobETagReader> mock = new Mock<IBlobETagReader>(MockBehavior.Strict);
            mock.Setup(r => r.GetETagAsync(It.Is<IStorageBlob>(b => b.BlobType == (StorageBlobType)expectedBlobType &&
                    b.Name == expectedBlobName && b.Container.Name == expectedContainerName),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult("ETag"))
                .Verifiable();
            IBlobETagReader eTagReader = mock.Object;
            BlobQueueTriggerExecutor product = CreateProductUnderTest(eTagReader);

            BlobQueueRegistration registration = new BlobQueueRegistration
            {
                BlobClient = CreateClient(),
                Executor = CreateDummyTriggeredFunctionExecutor()
            };
            product.Register(functionId, registration);

            BlobTriggerMessage triggerMessage = new BlobTriggerMessage
            {
                FunctionId = functionId,
                BlobType = (StorageBlobType)expectedBlobType,
                ContainerName = expectedContainerName,
                BlobName = expectedBlobName,
                ETag = "OriginalETag"
            };
            IStorageQueueMessage message = CreateMessage(triggerMessage);

            // Act
            Task task = product.ExecuteAsync(message, CancellationToken.None);

            // Assert
            task.WaitUntilCompleted();
            mock.Verify();
        }

        [Fact]
        public void ExecuteAsync_IfBlobHasBeenDeleted_ReturnsSuccessResult()
        {
            // Arrange
            string functionId = "FunctionId";
            IBlobETagReader eTagReader = CreateStubETagReader(null);
            BlobQueueTriggerExecutor product = CreateProductUnderTest(eTagReader);

            BlobQueueRegistration registration = new BlobQueueRegistration
            {
                BlobClient = CreateClient(),
                Executor = CreateDummyTriggeredFunctionExecutor()
            };
            product.Register(functionId, registration);

            IStorageQueueMessage message = CreateMessage(functionId, "OriginalETag");

            // Act
            Task<FunctionResult> task = product.ExecuteAsync(message, CancellationToken.None);

            // Assert
            Assert.True(task.Result.Succeeded);
        }

        [Fact]
        public void ExecuteAsync_IfBlobHasChanged_NotifiesWatcherAndReturnsSuccessResult()
        {
            // Arrange
            string functionId = "FunctionId";
            IBlobETagReader eTagReader = CreateStubETagReader("NewETag");
            Mock<IBlobWrittenWatcher> mock = new Mock<IBlobWrittenWatcher>(MockBehavior.Strict);
            mock.Setup(w => w.Notify(It.IsAny<IStorageBlob>()))
                .Verifiable();
            IBlobWrittenWatcher blobWrittenWatcher = mock.Object;
            BlobQueueTriggerExecutor product = CreateProductUnderTest(eTagReader, blobWrittenWatcher);

            BlobQueueRegistration registration = new BlobQueueRegistration
            {
                BlobClient = CreateClient(),
                Executor = CreateDummyTriggeredFunctionExecutor()
            };
            product.Register(functionId, registration);

            IStorageQueueMessage message = CreateMessage(functionId, "OriginalETag");

            // Act
            Task<FunctionResult> task = product.ExecuteAsync(message, CancellationToken.None);

            // Assert
            task.WaitUntilCompleted();
            mock.Verify();
            Assert.True(task.Result.Succeeded);
        }

        [Fact]
        public async Task ExecuteAsync_IfBlobIsUnchanged_CallsInnerExecutor()
        {
            // Arrange
            string functionId = "FunctionId";
            string matchingETag = "ETag";
            Guid expectedParentId = Guid.NewGuid();
            IStorageQueueMessage message = CreateMessage(functionId, matchingETag);
            IBlobETagReader eTagReader = CreateStubETagReader(matchingETag);
            IBlobCausalityReader causalityReader = CreateStubCausalityReader(expectedParentId);

            FunctionResult expectedResult = new FunctionResult(true);
            Mock<ITriggeredFunctionExecutor> mock = new Mock<ITriggeredFunctionExecutor>(MockBehavior.Strict);
            mock.Setup(e => e.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()))
                .Callback<TriggeredFunctionData, CancellationToken>(
                (mockInput, mockCancellationToken) =>
                {
                    Assert.Equal(expectedParentId, mockInput.ParentId);

                    StorageBlockBlob resultBlob = (StorageBlockBlob)mockInput.TriggerValue;
                    Assert.Equal(TestBlobName, resultBlob.Name);
                })
                .ReturnsAsync(expectedResult)
                .Verifiable();

            ITriggeredFunctionExecutor innerExecutor = mock.Object;
            BlobQueueTriggerExecutor product = CreateProductUnderTest(eTagReader, causalityReader);

            BlobQueueRegistration registration = new BlobQueueRegistration
            {
                BlobClient = CreateClient(),
                Executor = innerExecutor
            };
            product.Register(functionId, registration);

            // Act
            FunctionResult result = await product.ExecuteAsync(message, CancellationToken.None);

            // Assert
            Assert.Same(expectedResult, result);
            mock.Verify();
        }

        [Fact]
        public void ExecuteAsync_IfInnerExecutorSucceeds_ReturnsSuccessResult()
        {
            // Arrange
            string functionId = "FunctionId";
            string matchingETag = "ETag";
            IBlobETagReader eTagReader = CreateStubETagReader(matchingETag);
            IBlobCausalityReader causalityReader = CreateStubCausalityReader();

            FunctionResult expectedResult = new FunctionResult(true);
            Mock<ITriggeredFunctionExecutor> mock = new Mock<ITriggeredFunctionExecutor>(MockBehavior.Strict);
            mock.Setup(e => e.TryExecuteAsync(
                It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult)
                .Verifiable();

            BlobQueueTriggerExecutor product = CreateProductUnderTest(eTagReader, causalityReader);

            ITriggeredFunctionExecutor innerExecutor = mock.Object;
            BlobQueueRegistration registration = new BlobQueueRegistration
            {
                BlobClient = CreateClient(),
                Executor = innerExecutor
            };
            product.Register(functionId, registration);

            IStorageQueueMessage message = CreateMessage(functionId, matchingETag);

            // Act
            Task<FunctionResult> task = product.ExecuteAsync(message, CancellationToken.None);

            // Assert
            Assert.Same(expectedResult, task.Result);
        }

        [Fact]
        public void ExecuteAsync_IfInnerExecutorFails_ReturnsFailureResult()
        {
            // Arrange
            string functionId = "FunctionId";
            string matchingETag = "ETag";
            IBlobETagReader eTagReader = CreateStubETagReader(matchingETag);
            IBlobCausalityReader causalityReader = CreateStubCausalityReader();

            FunctionResult expectedResult = new FunctionResult(false);
            Mock<ITriggeredFunctionExecutor> mock = new Mock<ITriggeredFunctionExecutor>(MockBehavior.Strict);
            mock.Setup(e => e.TryExecuteAsync(
                It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult)
                .Verifiable();

            BlobQueueTriggerExecutor product = CreateProductUnderTest(eTagReader, causalityReader);

            ITriggeredFunctionExecutor innerExecutor = mock.Object;
            BlobQueueRegistration registration = new BlobQueueRegistration
            {
                BlobClient = CreateClient(),
                Executor = innerExecutor
            };
            product.Register(functionId, registration);

            IStorageQueueMessage message = CreateMessage(functionId, matchingETag);

            // Act
            Task<FunctionResult> task = product.ExecuteAsync(message, CancellationToken.None);

            // Assert
            Assert.False(task.Result.Succeeded);
        }

        private static IStorageBlobClient CreateClient()
        {
            StorageClientFactory clientFactory = new StorageClientFactory();

            IStorageAccount account = new StorageAccount(CloudStorageAccount.DevelopmentStorageAccount, clientFactory);
            return account.CreateBlobClient();
        }

        private static IBlobWrittenWatcher CreateDummyBlobWrittenWatcher()
        {
            return new Mock<IBlobWrittenWatcher>(MockBehavior.Strict).Object;
        }

        private static IBlobCausalityReader CreateDummyCausalityReader()
        {
            return new Mock<IBlobCausalityReader>(MockBehavior.Strict).Object;
        }

        private static IBlobETagReader CreateDummyETagReader()
        {
            return new Mock<IBlobETagReader>(MockBehavior.Strict).Object;
        }

        private static ITriggeredFunctionExecutor CreateDummyInnerExecutor()
        {
            return new Mock<ITriggeredFunctionExecutor>(MockBehavior.Strict).Object;
        }

        private static ITriggeredFunctionExecutor CreateDummyTriggeredFunctionExecutor()
        {
            return new Mock<ITriggeredFunctionExecutor>(MockBehavior.Strict).Object;
        }

        private static IStorageQueueMessage CreateMessage(string functionId, string eTag)
        {
            BlobTriggerMessage triggerMessage = new BlobTriggerMessage
            {
                FunctionId = functionId,
                BlobType = StorageBlobType.BlockBlob,
                ContainerName = "container",
                BlobName = TestBlobName,
                ETag = eTag
            };
            return CreateMessage(triggerMessage);
        }

        private static IStorageQueueMessage CreateMessage(BlobTriggerMessage triggerMessage)
        {
            return CreateMessage(JsonConvert.SerializeObject(triggerMessage));
        }

        private static IStorageQueueMessage CreateMessage(string content)
        {
            Mock<IStorageQueueMessage> mock = new Mock<IStorageQueueMessage>(MockBehavior.Strict);
            mock.Setup(m => m.AsString).Returns(content);
            return mock.Object;
        }

        private static BlobQueueTriggerExecutor CreateProductUnderTest()
        {
            return CreateProductUnderTest(CreateDummyETagReader());
        }

        private static BlobQueueTriggerExecutor CreateProductUnderTest(IBlobETagReader eTagReader)
        {
            return CreateProductUnderTest(eTagReader, CreateDummyBlobWrittenWatcher());
        }

        private static BlobQueueTriggerExecutor CreateProductUnderTest(IBlobETagReader eTagReader, IBlobWrittenWatcher blobWrittenWatcher)
        {
            IBlobCausalityReader causalityReader = CreateDummyCausalityReader();

            return CreateProductUnderTest(eTagReader, causalityReader, blobWrittenWatcher);
        }

        private static BlobQueueTriggerExecutor CreateProductUnderTest(IBlobETagReader eTagReader,
            IBlobCausalityReader causalityReader)
        {
            return CreateProductUnderTest(eTagReader, causalityReader, CreateDummyBlobWrittenWatcher());
        }

        private static BlobQueueTriggerExecutor CreateProductUnderTest(IBlobETagReader eTagReader,
             IBlobCausalityReader causalityReader, IBlobWrittenWatcher blobWrittenWatcher)
        {
            return new BlobQueueTriggerExecutor(eTagReader, causalityReader, blobWrittenWatcher);
        }

        private static IBlobCausalityReader CreateStubCausalityReader()
        {
            return CreateStubCausalityReader(null);
        }

        private static IBlobCausalityReader CreateStubCausalityReader(Guid? parentId)
        {
            Mock<IBlobCausalityReader> mock = new Mock<IBlobCausalityReader>(MockBehavior.Strict);
            mock.Setup(r => r.GetWriterAsync(It.IsAny<IStorageBlob>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(parentId));
            return mock.Object;
        }

        private static IBlobETagReader CreateStubETagReader(string eTag)
        {
            Mock<IBlobETagReader> mock = new Mock<IBlobETagReader>(MockBehavior.Strict);
            mock.Setup(r => r.GetETagAsync(It.IsAny<IStorageBlob>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<string>(eTag));
            return mock.Object;
        }

        private static IFunctionInstance CreateStubFunctionInstance(Guid? parentId)
        {
            Mock<IFunctionInstance> mock = new Mock<IFunctionInstance>(MockBehavior.Strict);
            mock.Setup(i => i.ParentId)
                .Returns(parentId);
            return mock.Object;
        }

        private static IFunctionExecutor CreateStubInnerExecutor(IDelayedException result)
        {
            Mock<IFunctionExecutor> mock = new Mock<IFunctionExecutor>(MockBehavior.Strict);
            mock.Setup(e => e.TryExecuteAsync(It.IsAny<IFunctionInstance>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(result));
            return mock.Object;
        }
    }
}
