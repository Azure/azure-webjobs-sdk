// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.ServiceBus.Messaging;
using Xunit;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests
{
    public class MessageProcessorTests
    {
        [Fact]
        public async Task CompleteProcessingMessageAsync_Failure_PropagatesException()
        {
            OnMessageOptions options = new OnMessageOptions
            {
                AutoComplete = false
            };
            MessageProcessor processor = new MessageProcessor(options);

            BrokeredMessage message = new BrokeredMessage();
            var functionException = new InvalidOperationException("Kaboom!");
            FunctionResult result = new FunctionResult(functionException);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await processor.CompleteProcessingMessageAsync(message, result, CancellationToken.None);
            });

            Assert.Same(functionException, ex);
        }

        [Fact]
        public async Task CompleteProcessingMessageAsync_DefaultOnMessageOptions()
        {
            MessageProcessor processor = new MessageProcessor(new OnMessageOptions());

            BrokeredMessage message = new BrokeredMessage();
            FunctionResult result = new FunctionResult(true);
            await processor.CompleteProcessingMessageAsync(message, result, CancellationToken.None);
        }

        [Fact]
        public void MessageOptions_ReturnsOptions()
        {
            OnMessageOptions options = new OnMessageOptions();
            MessageProcessor processor = new MessageProcessor(options);
            Assert.Same(options, processor.MessageOptions);
        }
    }
}