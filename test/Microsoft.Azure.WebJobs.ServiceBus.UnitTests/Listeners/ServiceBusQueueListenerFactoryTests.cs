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
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests.Listeners
{
    public class ServiceBusQueueListenerFactoryTests
    {
        [Fact]
        public async Task CreateAsync_Success()
        {
            var config = new ServiceBusConfiguration();
            var account = new ServiceBusAccount(config);
            Mock<ITriggeredFunctionExecutor> mockExecutor = new Mock<ITriggeredFunctionExecutor>(MockBehavior.Strict);
            ServiceBusQueueListenerFactory factory = new ServiceBusQueueListenerFactory(account, "testqueue", mockExecutor.Object, config);

            IListener listener = await factory.CreateAsync(CancellationToken.None);
            Assert.NotNull(listener);
        }
    }
}
