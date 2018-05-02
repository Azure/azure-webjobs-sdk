// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Cancellation;
using Microsoft.Azure.WebJobs.Host.Bindings.Data;
using Microsoft.Azure.WebJobs.Host.Bindings.Runtime;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Indexers
{
    internal sealed class DefaultBindingProvider : IBindingProviderFactory
    {
        private readonly INameResolver _nameResolver;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IExtensionRegistry _extensions;

        public DefaultBindingProvider(
            INameResolver nameResolver,
            ILoggerFactory loggerFactory,
            IExtensionRegistry extensions)
        {
            _nameResolver = nameResolver;
            _loggerFactory = loggerFactory;
            _extensions = extensions;
        }

        public IBindingProvider Create()
        {
            List<IBindingProvider> innerProviders = new List<IBindingProvider>();
                     
            // add any registered extension binding providers
            // Queue and Table bindings were added as an extension, so those rules get included here.  
            foreach (IBindingProvider provider in _extensions.GetExtensions(typeof(IBindingProvider)))
            {
                innerProviders.Add(provider);
            }
                        
            innerProviders.Add(new CancellationTokenBindingProvider());

            // The TraceWriter binder handles all remaining TraceWriter/TextWriter parameters. It must come after the
            // Blob binding provider; otherwise bindings like Do([Blob("a/b")] TextWriter blob) wouldn't work.
            innerProviders.Add(new TraceWriterBindingProvider(_loggerFactory));

            innerProviders.Add(new ILoggerBindingProvider(_loggerFactory));

            ContextAccessor<IBindingProvider> bindingProviderAccessor = new ContextAccessor<IBindingProvider>();
            innerProviders.Add(new RuntimeBindingProvider(bindingProviderAccessor));
            innerProviders.Add(new DataBindingProvider());

            IBindingProvider bindingProvider = new CompositeBindingProvider(innerProviders);
            bindingProviderAccessor.SetValue(bindingProvider);
            return bindingProvider;
        }
    }
}
