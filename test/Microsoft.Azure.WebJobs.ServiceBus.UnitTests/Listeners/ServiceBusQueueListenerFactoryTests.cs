// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.ServiceBus.Listeners;
using Microsoft.ServiceBus.Messaging;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests.Listeners
{
    public class ServiceBusQueueListenerFactoryTests
    {
        [Theory]
        [InlineData(AccessRights.Listen)]
        [InlineData(AccessRights.Send)]
        public async Task CreateAsync_AccessRightsNotManage_DoesNotCreateQueue(AccessRights accessRights)
        {
            ServiceBusAccount account = new ServiceBusAccount();
            Mock<ITriggeredFunctionExecutor> mockExecutor = new Mock<ITriggeredFunctionExecutor>(MockBehavior.Strict);
            ServiceBusConfiguration config = new ServiceBusConfiguration();
            var exceptionHandler = MessagingExceptionHandler.Subscribe(config.MessageOptions, new TestTraceWriter(TraceLevel.Verbose));
            ServiceBusQueueListenerFactory factory = new ServiceBusQueueListenerFactory(account, "testqueue", mockExecutor.Object, accessRights, new ServiceBusConfiguration(), exceptionHandler);

            IListener listener = await factory.CreateAsync(CancellationToken.None);
            Assert.NotNull(listener);
        }
    }
}
