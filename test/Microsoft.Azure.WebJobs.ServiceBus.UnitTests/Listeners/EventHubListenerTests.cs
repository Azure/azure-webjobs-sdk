// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using System;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests.Listeners
{
    public class EventHubListenerTests
    {
        [Theory]
        [InlineData(1, 100)]
        [InlineData(4, 25)]
        [InlineData(8, 12)]
        [InlineData(32, 3)]
        [InlineData(128, 0)]
        async Task EventHubUnorderedEventListener_CreatesCheckpointStrategy(int batchCheckpointFrequency, int expected)
        {
            var iterations = 100;
            var strategy = EventHubUnorderedEventListener.CreateCheckpointStrategy(batchCheckpointFrequency);

            var checkpoints = 0;
            Func<Task> checkpoint = () =>
            {
                checkpoints++;
                return Task.CompletedTask;
            };

            for (int i = 0; i < iterations; i++)
            {
                await strategy(checkpoint);
            }

            Assert.Equal(expected, checkpoints);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-2)]
        void EventHubUnorderedEventListener_Throws_IfInvalidCheckpointStrategy(int batchCheckpointFrequency)
        {
            var exc = Assert.Throws<InvalidOperationException>(() => EventHubUnorderedEventListener.CreateCheckpointStrategy(batchCheckpointFrequency));
            Assert.Equal("Batch listener checkpoint frequency must be larger than 0.", exc.Message);
        }

        [Theory]
        [InlineData(1, 100)]
        [InlineData(4, 25)]
        [InlineData(8, 12)]
        [InlineData(32, 3)]
        [InlineData(128, 0)]
        async Task EventHubOrderedEventListener_CreatesCheckpointStrategy(int batchCheckpointFrequency, int expected)
        {
            var iterations = 100;
            var strategy = EventHubOrderedEventListener.CreateCheckpointStrategy(batchCheckpointFrequency);

            var checkpoints = 0;
            Func<Task> checkpoint = () =>
            {
                checkpoints++;
                return Task.CompletedTask;
            };

            for (int i = 0; i < iterations; i++)
            {
                await strategy(checkpoint);
            }

            Assert.Equal(expected, checkpoints);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-2)]
        void EventHubOrderedEventListener_Throws_IfInvalidCheckpointStrategy(int batchCheckpointFrequency)
        {
            var exc = Assert.Throws<InvalidOperationException>(() => EventHubOrderedEventListener.CreateCheckpointStrategy(batchCheckpointFrequency));
            Assert.Equal("Stream listener checkpoint frequency must be larger than 0.", exc.Message);
        }
    }
}