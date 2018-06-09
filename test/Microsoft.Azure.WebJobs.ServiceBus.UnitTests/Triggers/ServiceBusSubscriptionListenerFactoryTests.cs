// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Azure.WebJobs.ServiceBus.Listeners;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests.Listeners
{
    public class ServiceBusSubscriptionListenerFactoryTests
    {
        [Fact]
        public async Task CreateAsync_Success()
        {
            var connectionStringProvider = TestHelpers.GetConnectionStringProvider();
            var config = new ServiceBusOptions
            {
                ConnectionString = connectionStringProvider.GetConnectionString(ConnectionStringNames.ServiceBus)
            };

            var messagingProvider = new MessagingProvider(new OptionsWrapper<ServiceBusOptions>(config));

            var account = new ServiceBusAccount(config, connectionStringProvider);
            Mock<ITriggeredFunctionExecutor> mockExecutor = new Mock<ITriggeredFunctionExecutor>(MockBehavior.Strict);
            ServiceBusSubscriptionListenerFactory factory = new ServiceBusSubscriptionListenerFactory(account, "testtopic", "testsubscription", mockExecutor.Object, config, messagingProvider);

            IListener listener = await factory.CreateAsync(CancellationToken.None);
            Assert.NotNull(listener);
        }
    }
}
