using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SampleHost
{
    public static class DynamicListenerServiceCollectionExtensions
    {
        public static IServiceCollection AddDynamicListeners(this IServiceCollection services)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IListenerDecorator, DynamicListenerDecorator>());
            services.TryAddSingleton<DynamicListenerManager>();
            services.AddSingleton<IDynamicListenerStatusProvider, DynamicListenerStatusProvider>();

            // We're wrapping the default IFunctionActivityStatusProvider using a bit of a hack to avoid a DI
            // stack overflow. The internal FunctionExecutor is the one implementing this service, so we're relying
            // on that knowledge here.
            // TODO: see if there's a better way to do this
            services.AddSingleton<IFunctionActivityStatusProvider>(provider => new CustomFunctionActivityStatusProvider((IFunctionActivityStatusProvider)provider.GetRequiredService<IFunctionExecutor>()));

            return services;
        }
    }
}
