// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Host.Queues.Listeners;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class JobHostQueuesConfigurationTests
    {
        [Fact]
        public void Constructor_Defaults()
        {
            // Arrange
            JobHostQueuesConfiguration config = new JobHostQueuesConfiguration();

            // Act & Assert
            Assert.Equal(16, config.BatchSize);
            Assert.Equal(8, config.NewBatchThreshold);
            Assert.Equal(typeof(DefaultQueueProcessorFactory), config.QueueProcessorFactory.GetType());
            Assert.Equal(QueuePollingIntervals.DefaultMaximum, config.MaxPollingInterval);
        }

        [Fact]
        public void NewBatchThreshold_CanSetAndGetValue()
        {
            // Arrange
            JobHostQueuesConfiguration config = new JobHostQueuesConfiguration();

            config.NewBatchThreshold = 1000;
            Assert.Equal(1000, config.NewBatchThreshold);
        }
    }
}
