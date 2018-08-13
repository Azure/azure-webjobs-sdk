// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.ServiceBus.Triggers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests.Bindings
{
    public class ServiceBusTriggerAttributeBindingProviderTests
    {
        private readonly Mock<MessagingProvider> _mockMessagingProvider;
        private readonly ServiceBusTriggerAttributeBindingProvider _provider;
        private readonly IConfiguration _configuration;

        public ServiceBusTriggerAttributeBindingProviderTests()
        {
            _configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();
            Mock<INameResolver> mockResolver = new Mock<INameResolver>(MockBehavior.Strict);

            ServiceBusOptions config = new ServiceBusOptions();
            _mockMessagingProvider = new Mock<MessagingProvider>(MockBehavior.Strict, new OptionsWrapper<ServiceBusOptions>(config));

            _provider = new ServiceBusTriggerAttributeBindingProvider(mockResolver.Object, config, _mockMessagingProvider.Object, _configuration);
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
