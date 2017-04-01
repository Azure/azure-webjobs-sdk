// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.Host.Indexers
{
    internal class FunctionIndexProvider : IFunctionIndexProvider
    {
        private Func<CancellationToken, Task<IFunctionIndex>> _initializeIndex;
        private IFunctionIndex _index;

        public FunctionIndexProvider(ITypeLocator typeLocator,
            ITriggerBindingProvider triggerBindingProvider,
            IBindingProvider bindingProvider,
            IJobActivator activator,
            IFunctionExecutor executor,
            IExtensionRegistry extensions,
            SingletonManager singletonManager,
            TraceWriter trace,
            IWebJobsExceptionHandler handler)
        {
            if (typeLocator == null)
            {
                throw new ArgumentNullException(nameof(typeLocator));
            }

            _initializeIndex = async (cancellationToken) =>
            {
                FunctionIndex index = new FunctionIndex();
                FunctionIndexer indexer = new FunctionIndexer(triggerBindingProvider, bindingProvider, activator, executor, extensions, singletonManager, trace, handler);

                // should we parallelize this?
                foreach (Type type in typeLocator.GetTypes())
                {
                    await indexer.IndexTypeAsync(type, index, cancellationToken);
                }

                return index;
            };
        }

        public async Task<IFunctionIndex> GetAsync(CancellationToken cancellationToken)
        {
            if (_index == null)
            {
                _index = await _initializeIndex(cancellationToken);
            }

            return _index;
        }
    }
}
