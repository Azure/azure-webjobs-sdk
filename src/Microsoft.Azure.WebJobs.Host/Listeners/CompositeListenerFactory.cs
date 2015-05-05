﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.Host.Listeners
{
    internal sealed class CompositeListenerFactory : IListenerFactory
    {
        private readonly IEnumerable<IListenerFactory> _listenerFactories;

        public CompositeListenerFactory(params IListenerFactory[] listenerFactories)
        {
            _listenerFactories = listenerFactories;
        }

        public async Task<IListener> CreateAsync(ListenerFactoryContext context)
        {
            List<IListener> listeners = new List<IListener>();

            foreach (IListenerFactory listenerFactory in _listenerFactories)
            {
                IListener listener = await listenerFactory.CreateAsync(context);
                listeners.Add(listener);
            }

            return new CompositeListener(listeners);
        }
    }
}
