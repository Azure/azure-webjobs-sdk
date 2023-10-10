using Microsoft.Azure.WebJobs.Host.Listeners;

namespace SampleHost
{
    internal class DynamicListenerDecorator : IListenerDecorator
    {
        private readonly DynamicListenerManager _dynamicListenerManager;

        public DynamicListenerDecorator(DynamicListenerManager dynamicListenerManager)
        {
            _dynamicListenerManager = dynamicListenerManager;
        }

        public IListener Decorate(ListenerDecoratorContext context)
        {
            if (_dynamicListenerManager.TryCreate(context.FunctionDefinition, context.Listener, out IListener dynamicListener))
            {
                // The listener will be managed dynamically. For the purposes of scale monitoring, the initial listener
                // will be used, even after it's stopped. I.e. for new dynamic listeners created internally, they
                // don't need to be registered with scale monitoring.
                return dynamicListener;
            }

            return context.Listener;
        }
    }
}
