// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests
{
    public class ServiceBusAccountTests
    {
        private readonly IConnectionStringProvider _connectionStringProvider;

        public ServiceBusAccountTests()
        {
            _connectionStringProvider = TestHelpers.GetConnectionStringProvider();
        }

        [Fact]
        public void GetConnectionString_ReturnsExpectedConnectionString()
        {
            string defaultConnection = "Endpoint=sb://default.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123=";
            var config = new ServiceBusOptions()
            {
                ConnectionString = defaultConnection
            };
            var attribute = new ServiceBusTriggerAttribute("entity-name");
            var account = new ServiceBusAccount(config, _connectionStringProvider, attribute);

            Assert.True(defaultConnection == account.ConnectionString);
        }

        [Fact]
        public void GetConnectionString_ThrowsIfConnectionStringNullOrEmpty()
        {
            var config = new ServiceBusOptions();
            var attribute = new ServiceBusTriggerAttribute("testqueue");
            attribute.Connection = "MissingConnection";

            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                var account = new ServiceBusAccount(config, _connectionStringProvider, attribute);
                var cs = account.ConnectionString;
            });
            Assert.Equal("Microsoft Azure WebJobs SDK ServiceBus connection string 'MissingConnection' is missing or empty.", ex.Message);

            attribute.Connection = null;
            config.ConnectionString = null;
            ex = Assert.Throws<InvalidOperationException>(() =>
            {
                var account = new ServiceBusAccount(config, _connectionStringProvider, attribute);
                var cs = account.ConnectionString;
            });
            Assert.Equal("Microsoft Azure WebJobs SDK ServiceBus connection string 'AzureWebJobsServiceBus' is missing or empty.", ex.Message);
        }
    }
}
