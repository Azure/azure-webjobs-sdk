﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Xunit;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests
{
    public class MessagingProviderTests : IDisposable
    {
        private readonly ServiceBusConfiguration _config;
        private readonly MessagingProvider _provider;

        public MessagingProviderTests()
        {
            string defaultConnection = "Endpoint=sb://default.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123=";
            _config = new ServiceBusConfiguration
            {
                ConnectionString = defaultConnection
            };
            _provider = new MessagingProvider(_config);

            string overrideConnection = "Endpoint=sb://override.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123=";
            Environment.SetEnvironmentVariable("AzureWebJobsServiceBusOverride", overrideConnection);
        }

        [Fact]
        public void CreateMessagingFactoryAsync_ReturnsExpectedFactory()
        {
            // default connection
            MessagingFactory factory = _provider.CreateMessagingFactory("test");
            Assert.Equal("default.servicebus.windows.net", factory.Address.Host);

            // override connection
            factory = _provider.CreateMessagingFactory("test", "ServiceBusOverride");
            Assert.Equal("override.servicebus.windows.net", factory.Address.Host);
        }

        [Fact]
        public void CreateNamespaceManager_ReturnsExpectedManager()
        {
            // default connection
            NamespaceManager manager = _provider.CreateNamespaceManager();
            Assert.Equal("default.servicebus.windows.net", manager.Address.Host);

            // override connection
            manager = _provider.CreateNamespaceManager("ServiceBusOverride");
            Assert.Equal("override.servicebus.windows.net", manager.Address.Host);
        }

        [Fact]
        public void CreateMessageReceiver_ReturnsExpectedReceiver()
        {
            MessagingFactory factory = _provider.CreateMessagingFactory("test");
            MessageReceiver receiver = _provider.CreateMessageReceiver(factory, "test");
            Assert.Equal("test", receiver.Path);

            _config.PrefetchCount = 100;
            receiver = _provider.CreateMessageReceiver(factory, "test");
            Assert.Equal(100, receiver.PrefetchCount);
        }

        [Fact]
        public void CreateMessagingFactory_CachesPerConnection()
        {
            var factory = _provider.CreateMessagingFactory("test");

            // same cached factory each time for the same entity/connection
            Assert.Same(factory, _provider.CreateMessagingFactory("test"));
            Assert.Same(factory, _provider.CreateMessagingFactory("test"));
            Assert.Same(factory, _provider.CreateMessagingFactory("test"));

            // different factory for a different entity
            var factory2 = _provider.CreateMessagingFactory("test2");
            Assert.NotSame(factory, factory2);
            Assert.Same(factory2, _provider.CreateMessagingFactory("test2"));

            factory2 = _provider.CreateMessagingFactory("test3", "ServiceBusOverride");

            // same cached factory each time for the same entity/connection
            Assert.Same(factory2, _provider.CreateMessagingFactory("test3", "ServiceBusOverride"));
            Assert.Same(factory2, _provider.CreateMessagingFactory("test3", "ServiceBusOverride"));
            Assert.Same(factory2, _provider.CreateMessagingFactory("test3", "ServiceBusOverride"));

            // different factory for same entity different connection
            var factory3 = _provider.CreateMessagingFactory("test3", "AzureWebJobsServiceBus");
            Assert.NotSame(factory2, factory3);
            Assert.Same(factory3, _provider.CreateMessagingFactory("test3", "AzureWebJobsServiceBus"));
        }

        [Fact]
        public void GetConnectionString_ThrowsIfConnectionStringNullOrEmpty()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                _provider.GetConnectionString("MissingConnection");
            });
            Assert.Equal("Microsoft Azure WebJobs SDK ServiceBus connection string 'AzureWebJobsMissingConnection' is missing or empty.", ex.Message);

            _config.ConnectionString = null;
            ex = Assert.Throws<InvalidOperationException>(() =>
            {
                _provider.GetConnectionString(null);
            });
            Assert.Equal("Microsoft Azure WebJobs SDK ServiceBus connection string 'AzureWebJobsServiceBus' is missing or empty.", ex.Message);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("AzureWebJobsServiceBusOverride", null);
        }
    }
}
