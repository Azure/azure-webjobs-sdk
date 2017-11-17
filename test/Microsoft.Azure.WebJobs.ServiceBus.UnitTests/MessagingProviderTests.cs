// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Azure.ServiceBus.Core;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests
{
    public class MessagingProviderTests
    {
        [Fact]
        public void CreateMessageProcessor_ReturnsExpectedReceiver()
        {
            string defaultConnection = "Endpoint=sb://default.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123=";
            var config = new ServiceBusConfiguration
            {
                ConnectionString = defaultConnection
            };
            var provider = new MessagingProvider(config);
            var receiver = provider.CreateMessageReceiver("entityPath", defaultConnection);
            Assert.Equal("entityPath", receiver.Path);

            config.PrefetchCount = 100;
            receiver = provider.CreateMessageReceiver("entityPath1", defaultConnection);
            Assert.Equal(100, receiver.PrefetchCount);
        }
    }
}
