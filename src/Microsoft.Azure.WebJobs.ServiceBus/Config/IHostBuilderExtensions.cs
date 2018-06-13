using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Azure.WebJobs.ServiceBus.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.Hosting
{
    public static class IHostBuilderExtensions
    {
        public static IHostBuilder AddServiceBus(this IHostBuilder builder)
        {
            return builder.AddExtension<ServiceBusExtensionConfig>()
                .ConfigureServices(services =>
                {
                    services.AddOptions<ServiceBusOptions>()
                            .Configure<IConnectionStringProvider>((o, p) =>
                            {
                                o.ConnectionString = p.GetConnectionString(ConnectionStringNames.ServiceBus);
                            });

                    services.TryAddSingleton<MessagingProvider>();
                });
        }
    }
}
