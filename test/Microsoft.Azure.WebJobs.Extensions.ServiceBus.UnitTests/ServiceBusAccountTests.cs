// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests
{
    public class ServiceBusAccountTests
    {
        private readonly IConfiguration _configuration;

        public ServiceBusAccountTests()
        {
            _configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();
        }

        [Theory]
        [InlineData(
            "Endpoint=sb://default.servicebus.windows.net;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123=",
            "Endpoint=sb://default.servicebus.windows.net;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123=",
            "entity-name")]
        [InlineData(
            "Endpoint=sb://default.servicebus.windows.net;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123=;EntityPath=entity-name-cs",
            "Endpoint=sb://default.servicebus.windows.net;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123=",
            "entity-name-cs")]
        public void GetConnectionString_ReturnsExpectedConnectionString(string connectionStringInitial, string connectionString, string entityName)
        {
            var options = new ServiceBusOptions()
            {
                ConnectionString = connectionStringInitial
            };
            ServiceBusTriggerAttribute attribute = new ServiceBusTriggerAttribute(entityName);
            var account = new ServiceBusAccount(options, _configuration, entityName, attribute);

            Assert.Equal(connectionString, account.ConnectionString);
            Assert.Equal(entityName, account.EntityPath);
        }

        [Fact]
        public void GetConnectionString_ThrowsIfConnectionStringNullOrEmpty()
        {
            var config = new ServiceBusOptions();
            var attribute = new ServiceBusTriggerAttribute("testqueue");
            attribute.Connection = "MissingConnection";

            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                var account = new ServiceBusAccount(config, _configuration, "testqueue", attribute);
                var cs = account.ConnectionString;
            });
            Assert.Equal("Microsoft Azure WebJobs SDK ServiceBus connection string 'MissingConnection' is missing or empty.", ex.Message);

            attribute.Connection = null;
            config.ConnectionString = null;
            ex = Assert.Throws<InvalidOperationException>(() =>
            {
                var account = new ServiceBusAccount(config, _configuration, "testqueue", attribute);
                var cs = account.ConnectionString;
            });
            Assert.Equal("Microsoft Azure WebJobs SDK ServiceBus connection string 'AzureWebJobsServiceBus' is missing or empty.", ex.Message);
        }
    }
}
