using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Azure.WebJobs.ServiceBus.Config;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WebJobs.ServiceBus.Config;

namespace Microsoft.Extensions.Hosting
{
    public static class IHostBuilderExtensions
    {
        public static IHostBuilder AddServiceBus(this IHostBuilder builder)
        {
            return builder.AddExtension<ServiceBusExtensionConfig>()
                .ConfigureServices(services =>
                {
                    services.TryAddSingleton<IConfigureOptions<ServiceBusOptions>, ServiceBusOptionsFactory>();
                    services.TryAddSingleton<IMessagingProvider, MessagingProvider>();
                });
        }
    }
}
