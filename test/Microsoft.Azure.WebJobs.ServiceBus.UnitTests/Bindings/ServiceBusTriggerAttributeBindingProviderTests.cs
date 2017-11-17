// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.ServiceBus.Triggers;
using Microsoft.Azure.ServiceBus;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests.Bindings
{
    public class ServiceBusTriggerAttributeBindingProviderTests
    {
        private readonly Mock<MessagingProvider> _mockMessagingProvider;
        private readonly ServiceBusTriggerAttributeBindingProvider _provider;

        public ServiceBusTriggerAttributeBindingProviderTests()
        {
            Mock<INameResolver> mockResolver = new Mock<INameResolver>(MockBehavior.Strict);

            ServiceBusConfiguration config = new ServiceBusConfiguration();
            _mockMessagingProvider = new Mock<MessagingProvider>(MockBehavior.Strict, config);

            config.MessagingProvider = _mockMessagingProvider.Object;
            _provider = new ServiceBusTriggerAttributeBindingProvider(mockResolver.Object, config);
        }

        [Fact]
        public async Task TryCreateAsync_AccountOverride_OverrideIsApplied()
        {
            ParameterInfo parameter = GetType().GetMethod("TestJob_AccountOverride").GetParameters()[0];
            TriggerBindingProviderContext context = new TriggerBindingProviderContext(parameter, CancellationToken.None);

            ITriggerBinding binding = await _provider.TryCreateAsync(context);

            Assert.NotNull(binding);
        }

        [Fact]
        public async Task TryCreateAsync_DefaultAccount()
        {
            ParameterInfo parameter = GetType().GetMethod("TestJob").GetParameters()[0];
            TriggerBindingProviderContext context = new TriggerBindingProviderContext(parameter, CancellationToken.None);

            ITriggerBinding binding = await _provider.TryCreateAsync(context);

            Assert.NotNull(binding);
        }

        public static void TestJob_AccountOverride(
            [ServiceBusTriggerAttribute("test"),
             ServiceBusAccount("testaccount")] Message message)
        {
            message = new Message();
        }

        public static void TestJob(
            [ServiceBusTriggerAttribute("test")] Message message)
        {
            message = new Message();
        }
    }
}
