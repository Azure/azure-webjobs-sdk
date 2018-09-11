// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.EventHubs.UnitTests
{
    public class EventHubListenerTests
    {
        [Theory]
        [InlineData(1, 100)]
        [InlineData(4, 25)]
        [InlineData(8, 12)]
        [InlineData(32, 3)]
        [InlineData(128, 0)]
        public async Task ProcessEvents_SingleDispatch_CheckpointsCorrectly(int batchCheckpointFrequency, int expected)
        {
            var partitionContext = EventHubTests.GetPartitionContext();
            var checkpoints = 0;
            var config = new EventHubConfiguration
            {
                BatchCheckpointFrequency = batchCheckpointFrequency
            };
            var checkpointer = new Mock<EventHubListener.ICheckpointer>(MockBehavior.Strict);
            checkpointer.Setup(p => p.CheckpointAsync(partitionContext)).Callback<PartitionContext>(c =>
            {
                checkpoints++;
            }).Returns(Task.CompletedTask);
            var loggerMock = new Mock<ILogger>(MockBehavior.Strict);
            var executor = new Mock<ITriggeredFunctionExecutor>(MockBehavior.Strict);
            executor.Setup(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>())).ReturnsAsync(new FunctionResult(true));
            var eventProcessor = new EventHubListener.EventProcessor(config, executor.Object, loggerMock.Object, true, checkpointer.Object);

            for (int i = 0; i < 100; i++)
            {
                List<EventData> events = new List<EventData>() { new EventData(new byte[0]) };
                await eventProcessor.ProcessEventsAsync(partitionContext, events);
            }

            Assert.Equal(expected, checkpoints);
        }

        [Theory]
        [InlineData(1, 100)]
        [InlineData(4, 25)]
        [InlineData(8, 12)]
        [InlineData(32, 3)]
        [InlineData(128, 0)]
        public async Task ProcessEvents_MultipleDispatch_CheckpointsCorrectly(int batchCheckpointFrequency, int expected)
        {
            var partitionContext = EventHubTests.GetPartitionContext();
            var config = new EventHubConfiguration
            {
                BatchCheckpointFrequency = batchCheckpointFrequency
            };
            var checkpointer = new Mock<EventHubListener.ICheckpointer>(MockBehavior.Strict);
            checkpointer.Setup(p => p.CheckpointAsync(partitionContext)).Returns(Task.CompletedTask);
            var loggerMock = new Mock<ILogger>(MockBehavior.Strict);
            var executor = new Mock<ITriggeredFunctionExecutor>(MockBehavior.Strict);
            executor.Setup(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>())).ReturnsAsync(new FunctionResult(true));
            var eventProcessor = new EventHubListener.EventProcessor(config, executor.Object, loggerMock.Object, false, checkpointer.Object);

            for (int i = 0; i < 100; i++)
            {
                List<EventData> events = new List<EventData>() { new EventData(new byte[0]), new EventData(new byte[0]), new EventData(new byte[0]) };
                await eventProcessor.ProcessEventsAsync(partitionContext, events);
            }

            checkpointer.Verify(p => p.CheckpointAsync(partitionContext), Times.Exactly(expected));
        }

        /// <summary>
        /// Even if some events in a batch fail, we still checkpoint. Event error handling
        /// is the responsiblity of user function code.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ProcessEvents_Failure_Checkpoints()
        {
            var partitionContext = EventHubTests.GetPartitionContext();
            var config = new EventHubConfiguration();
            var checkpointer = new Mock<EventHubListener.ICheckpointer>(MockBehavior.Strict);
            checkpointer.Setup(p => p.CheckpointAsync(partitionContext)).Returns(Task.CompletedTask);

            List<EventData> events = new List<EventData>();
            List<FunctionResult> results = new List<FunctionResult>();
            for (int i = 0; i < 10; i++)
            {
                events.Add(new EventData(new byte[0]));
                var succeeded = i > 7 ? false : true;
                results.Add(new FunctionResult(succeeded));
            }

            var executor = new Mock<ITriggeredFunctionExecutor>(MockBehavior.Strict);
            int execution = 0;
            executor.Setup(p => p.TryExecuteAsync(It.IsAny<TriggeredFunctionData>(), It.IsAny<CancellationToken>())).ReturnsAsync(() =>
            {
                var result = results[execution++];
                return result;
            });
            var loggerMock = new Mock<ILogger>(MockBehavior.Strict);
            var eventProcessor = new EventHubListener.EventProcessor(config, executor.Object, loggerMock.Object, true, checkpointer.Object);

            await eventProcessor.ProcessEventsAsync(partitionContext, events);

            checkpointer.Verify(p => p.CheckpointAsync(partitionContext), Times.Once);
        }

        [Fact]
        public async Task CloseAsync_Shutdown_DoesNotCheckpoint()
        {
            var partitionContext = EventHubTests.GetPartitionContext();
            var config = new EventHubConfiguration();
            var checkpointer = new Mock<EventHubListener.ICheckpointer>(MockBehavior.Strict);
            var executor = new Mock<ITriggeredFunctionExecutor>(MockBehavior.Strict);
            var loggerMock = new Mock<ILogger>(MockBehavior.Strict);
            var eventProcessor = new EventHubListener.EventProcessor(config, executor.Object, loggerMock.Object, true, checkpointer.Object);

            await eventProcessor.CloseAsync(partitionContext, CloseReason.Shutdown);

            checkpointer.Verify(p => p.CheckpointAsync(partitionContext), Times.Never);
        }
    }
}
