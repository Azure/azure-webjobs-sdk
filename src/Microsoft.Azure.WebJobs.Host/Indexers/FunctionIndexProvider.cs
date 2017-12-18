// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Dispatch;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Indexers
{
    internal class FunctionIndexProvider : IFunctionIndexProvider
    {
        private readonly ITypeLocator _typeLocator;
        private readonly ITriggerBindingProvider _triggerBindingProvider;
        private readonly IBindingProvider _bindingProvider;
        private readonly IJobActivator _activator;
        private readonly IFunctionExecutor _executor;
        private readonly IExtensionRegistry _extensions;
        private readonly SingletonManager _singletonManager;
        private readonly ILoggerFactory _loggerFactory;
        private readonly SharedQueueHandler _sharedQueue;

        private IFunctionIndex _index;

        public FunctionIndexProvider(ITypeLocator typeLocator,
            ITriggerBindingProvider triggerBindingProvider,
            IBindingProvider bindingProvider,
            IJobActivator activator,
            IFunctionExecutor executor,
            IExtensionRegistry extensions,
            SingletonManager singletonManager,
            ILoggerFactory loggerFactory,
            SharedQueueHandler sharedQueue)
        {

            _typeLocator = typeLocator ?? throw new ArgumentNullException(nameof(typeLocator));
            _triggerBindingProvider = triggerBindingProvider ?? throw new ArgumentNullException(nameof(triggerBindingProvider));
            _bindingProvider = bindingProvider ?? throw new ArgumentNullException(nameof(bindingProvider));
            _activator = activator ?? throw new ArgumentNullException(nameof(activator));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _extensions = extensions ?? throw new ArgumentNullException(nameof(extensions));
            _singletonManager = singletonManager ?? throw new ArgumentNullException(nameof(singletonManager));
            _sharedQueue = sharedQueue ?? throw new ArgumentNullException(nameof(sharedQueue));

            _loggerFactory = loggerFactory;
        }

        public async Task<IFunctionIndex> GetAsync(CancellationToken cancellationToken)
        {
            if (_index == null)
            {
                _index = await CreateAsync(cancellationToken);
            }

            return _index;
        }

        private async Task<IFunctionIndex> CreateAsync(CancellationToken cancellationToken)
        {
            FunctionIndex index = new FunctionIndex();
            FunctionIndexer indexer = new FunctionIndexer(_triggerBindingProvider, _bindingProvider, _activator, _executor, _extensions, _singletonManager, _loggerFactory, null, _sharedQueue);
            IReadOnlyList<Type> types = _typeLocator.GetTypes();

            foreach (Type type in types)
            {
                await indexer.IndexTypeAsync(type, index, cancellationToken);
            }

            return index;
        }
    }
}
