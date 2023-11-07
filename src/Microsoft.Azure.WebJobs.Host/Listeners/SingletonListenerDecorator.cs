// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Listeners
{
    internal class SingletonListenerDecorator : IListenerDecorator
    {
        private readonly SingletonManager _singletonManager;
        private readonly ILoggerFactory _loggerFactory;

        public SingletonListenerDecorator(SingletonManager singletonManager, ILoggerFactory loggerFactory) 
        {
            _singletonManager = singletonManager;
            _loggerFactory = loggerFactory;
        }

        public IListener Decorate(ListenerDecoratorContext context)
        {
            var functionDescriptor = context.FunctionDefinition.Descriptor;

            // if the listener is a Singleton, wrap it with our SingletonListener
            IListener listener = context.Listener;
            SingletonAttribute singletonAttribute = SingletonManager.GetListenerSingletonOrNull(context.ListenerType, functionDescriptor);
            if (singletonAttribute != null)
            {
                listener = new SingletonListener(functionDescriptor, singletonAttribute, _singletonManager, listener, _loggerFactory);
            }

            return listener;
        }
    }
}
