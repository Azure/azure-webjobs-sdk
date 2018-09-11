// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.ServiceBus.Listeners;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests.Listeners
{
    public class ServiceBusListenerTests
    {
        //private readonly MessagingFactory _messagingFactory;
        private readonly ServiceBusListener _listener;
        private readonly Mock<ITriggeredFunctionExecutor> _mockExecutor;
        private readonly Mock<MessagingProvider> _mockMessagingProvider;
        private readonly Mock<MessageProcessor> _mockMessageProcessor;
        private readonly string _entityPath = "test-entity-path";
        private readonly string _testConnection = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123=";

        public ServiceBusListenerTests()
        {
            _mockExecutor = new Mock<ITriggeredFunctionExecutor>(MockBehavior.Strict);

            MessageHandlerOptions messageOptions = new MessageHandlerOptions(ExceptionReceivedHandler);
            MessageReceiver messageReceiver = new MessageReceiver(_testConnection, _entityPath);
            _mockMessageProcessor = new Mock<MessageProcessor>(MockBehavior.Strict, messageReceiver, messageOptions);

            ServiceBusOptions config = new ServiceBusOptions
            {
                MessageHandlerOptions = messageOptions
            };
            _mockMessagingProvider = new Mock<MessagingProvider>(MockBehavior.Strict, new OptionsWrapper<ServiceBusOptions>(config));

            _mockMessagingProvider.Setup(p => p.CreateMessageProcessor(_entityPath, _testConnection))
                .Returns(_mockMessageProcessor.Object);

            ServiceBusTriggerExecutor triggerExecutor = new ServiceBusTriggerExecutor(_mockExecutor.Object);
            var mockServiceBusAccount = new Mock<ServiceBusAccount>(MockBehavior.Strict);
            mockServiceBusAccount.Setup(a => a.ConnectionString).Returns(_testConnection);

            _listener = new ServiceBusListener(_entityPath, triggerExecutor, config, mockServiceBusAccount.Object, _mockMessagingProvider.Object);
        }

        [Fact]
        public async Task ProcessMessageAsync_Success()
        {
            var message = new CustomMessage();
            message.MessageId = Guid.NewGuid().ToString();
            CancellationToken cancellationToken = new CancellationToken();
            _mockMessageProcessor.Setup(p => p.BeginProcessingMessageAsync(message, cancellationToken)).ReturnsAsync(true);

            FunctionResult result = new FunctionResult(true);
            _mockExecutor.Setup(p => p.TryExecuteAsync(It.Is<TriggeredFunctionData>(q => q.TriggerValue == message), cancellationToken)).ReturnsAsync(result);

            _mockMessageProcessor.Setup(p => p.CompleteProcessingMessageAsync(message, result, cancellationToken)).Returns(Task.FromResult(0));

            await _listener.ProcessMessageAsync(message, CancellationToken.None);

            _mockMessageProcessor.VerifyAll();
            _mockExecutor.VerifyAll();
            _mockMessageProcessor.VerifyAll();
        }

        [Fact]
        public async Task ProcessMessageAsync_BeginProcessingReturnsFalse_MessageNotProcessed()
        {
            var message = new CustomMessage();
            message.MessageId = Guid.NewGuid().ToString();
            CancellationToken cancellationToken = new CancellationToken();
            _mockMessageProcessor.Setup(p => p.BeginProcessingMessageAsync(message, cancellationToken)).ReturnsAsync(false);

            await _listener.ProcessMessageAsync(message, CancellationToken.None);

            _mockMessageProcessor.VerifyAll();
        }

        Task ExceptionReceivedHandler(ExceptionReceivedEventArgs eventArgs)
        {
            return Task.CompletedTask;
        }
    }

    // Mock calls ToString() for Mesage. This ckass fixes bug in azure-service-bus-dotnet.
    // https://github.com/Azure/azure-service-bus-dotnet/blob/dev/src/Microsoft.Azure.ServiceBus/Message.cs#L291
    internal class CustomMessage : Message
    {
        public override string ToString()
        {
            return MessageId;
        }
    }
}
