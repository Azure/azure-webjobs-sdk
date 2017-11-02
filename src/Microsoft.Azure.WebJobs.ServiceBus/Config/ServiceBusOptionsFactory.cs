using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Extensions.Options;

namespace WebJobs.ServiceBus.Config
{
    public class ServiceBusOptionsFactory : IConfigureOptions<ServiceBusOptions>
    {
        private readonly IConnectionStringProvider _connectionStringProvider;

        public ServiceBusOptionsFactory(IConnectionStringProvider connectionStringProvider)
        {
            _connectionStringProvider = connectionStringProvider;
        }

        public void Configure(ServiceBusOptions options)
        {
            options.ConnectionString = _connectionStringProvider.GetConnectionString(ConnectionStringNames.ServiceBus);
        }
    }
}
