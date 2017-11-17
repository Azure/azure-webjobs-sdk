// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.ServiceBus.Bindings;
using Microsoft.Azure.ServiceBus;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests.Bindings
{
    public class ServiceBusAttributeBindingProviderTests
    {
        private readonly Mock<MessagingProvider> _mockMessagingProvider;
        private readonly ServiceBusAttributeBindingProvider _provider;

        public ServiceBusAttributeBindingProviderTests()
        {
            Mock<INameResolver> mockResolver = new Mock<INameResolver>(MockBehavior.Strict);

            ServiceBusConfiguration config = new ServiceBusConfiguration();
            _mockMessagingProvider = new Mock<MessagingProvider>(MockBehavior.Strict, config);
            
            config.MessagingProvider = _mockMessagingProvider.Object;
            _provider = new ServiceBusAttributeBindingProvider(mockResolver.Object, config);
        }

        [Fact]
        public async Task TryCreateAsync_AccountOverride_OverrideIsApplied()
        {

            ParameterInfo parameter = GetType().GetMethod("TestJob_AccountOverride").GetParameters()[0];
            BindingProviderContext context = new BindingProviderContext(parameter, new Dictionary<string, Type>(), CancellationToken.None);

            IBinding binding = await _provider.TryCreateAsync(context);

            Assert.NotNull(binding);
        }

        [Fact]
        public async Task TryCreateAsync_DefaultAccount()
        {

            ParameterInfo parameter = GetType().GetMethod("TestJob").GetParameters()[0];
            BindingProviderContext context = new BindingProviderContext(parameter, new Dictionary<string, Type>(), CancellationToken.None);

            IBinding binding = await _provider.TryCreateAsync(context);

            Assert.NotNull(binding);
        }

        public static void TestJob_AccountOverride(
            [ServiceBusAttribute("test"), 
             ServiceBusAccount("testaccount")] out Message message)
        {
            message = new Message();
        }

        public static void TestJob(
            [ServiceBusAttribute("test")] out Message message)
        {
            message = new Message();
        }
    }
}
