// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.WebJobs.Host.Executors;
using Xunit;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests
{
    public class MessageProcessorTests
    {
        private readonly MessageProcessor _processor;
        private readonly MessageHandlerOptions _options;

        public MessageProcessorTests()
        {
            _options = new MessageHandlerOptions(ExceptionReceivedHandler);
            MessageReceiver receiver = new MessageReceiver("Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123=", "test-entity");
            _processor = new MessageProcessor(receiver, _options);
        }

        [Fact]
        public async Task CompleteProcessingMessageAsync_Success_CompletesMessage_WhenAutoCompleteFalse()
        {
            _options.AutoComplete = false;

            Message message = new Message();
            FunctionResult result = new FunctionResult(true);
            var ex = await Assert.ThrowsAsync<FormatException>(async () =>
            {
                await _processor.CompleteProcessingMessageAsync(message, result, CancellationToken.None);
            });

            // The service bus APIs aren't unit testable, so in this test suite
            // we rely on exception stacks to verify APIs are called as expected.
            // this verifies that we initiated the completion
            Assert.True(ex.ToString().Contains("Microsoft.Azure.ServiceBus.Core.MessageReceiver.OnCompleteAsync"));
        }

        [Fact]
        public async Task CompleteProcessingMessageAsync_Failure_PropagatesException()
        {
            _options.AutoComplete = false;

            Message message = new Message();
            var functionException = new InvalidOperationException("Kaboom!");
            FunctionResult result = new FunctionResult(functionException);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _processor.CompleteProcessingMessageAsync(message, result, CancellationToken.None);
            });

            Assert.Same(functionException, ex);
        }

        [Fact]
        public async Task CompleteProcessingMessageAsync_DefaultOnMessageOptions()
        {
            Message message = new Message();
            FunctionResult result = new FunctionResult(true);
            await _processor.CompleteProcessingMessageAsync(message, result, CancellationToken.None);
        }

        [Fact]
        public void MessageOptions_ReturnsOptions()
        {
            Assert.Same(_options, _processor.MessageOptions);
        }

        Task ExceptionReceivedHandler(ExceptionReceivedEventArgs eventArgs)
        {
            return Task.CompletedTask;
        }
    }
}