using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests
{
    public class ServiceBusAccountTests
    {
        [Fact]
        public void GetConnectionString_ReturnsExpectedConnectionString()
        {
            string defaultConnection = "Endpoint=sb://default.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123=";
            var config = new ServiceBusConfiguration()
            {
                ConnectionString = defaultConnection
            };
            var attribute = new ServiceBusTriggerAttribute("entity-name");
            var account = new ServiceBusAccount(config, attribute);

            Assert.True(defaultConnection == account.ConnectionString);
        }

        [Fact]
        public void GetConnectionString_ThrowsIfConnectionStringNullOrEmpty()
        { 
            var config = new ServiceBusConfiguration();
            var attribute = new ServiceBusTriggerAttribute("testqueue");
            attribute.Connection = "MissingConnection";

            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                var account = new ServiceBusAccount(config, attribute);
                var cs = account.ConnectionString;
            });
            Assert.Equal("Microsoft Azure WebJobs SDK ServiceBus connection string 'AzureWebJobsMissingConnection' is missing or empty.", ex.Message);

            attribute.Connection = null;
            config.ConnectionString = null;
            ex = Assert.Throws<InvalidOperationException>(() =>
            {
                var account = new ServiceBusAccount(config, attribute);
                var cs = account.ConnectionString;
            });
            Assert.Equal("Microsoft Azure WebJobs SDK ServiceBus connection string 'AzureWebJobsServiceBus' is missing or empty.", ex.Message);
        }
    }
}
