// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using System.Threading;
using Microsoft.Azure.WebJobs.Host.Config;
using System.IO;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests
{
    // Fake Queue support. 100% in-memory for unit test bindings. 
    // Put on a parameter to mark that it goes to a "FakeQueue". 
    public class FakeQueueAttribute : Attribute
    {

    }

    public class FakeQueueClient : IFlushCollector<FakeQueueData>, IExtensionConfigProvider
    {
        public List<FakeQueueData> _items = new List<FakeQueueData>();

        public Task AddAsync(FakeQueueData item, CancellationToken cancellationToken = default(CancellationToken))
        {
            _items.Add(item);
            return Task.FromResult(0);
        }

        public Task FlushAsync()
        {
            return Task.FromResult(0);
        }

        public void Initialize(ExtensionConfigContext context)
        {
            IConverterManager cm = context.Config.GetOrCreateConverterManager();
            cm.AddConverter<string, FakeQueueData>(x => new FakeQueueData { Message = x });

           IExtensionRegistry extensions = context.Config.GetService<IExtensionRegistry>();

            var bindingProvider = new FakeQueueBindingProvider(this, cm);
            extensions.RegisterExtension<IBindingProvider>(bindingProvider);
        }
    }

    // Fake queue message. Equivalent of CloudQueueMessage or EventData
    public class FakeQueueData
    {
        // This correpsonds to string & poco conversion. 
        public string Message { get; set; }

        // Advanced property not captured with JSON serialization. 
        public string ExtraPropertery { get; set; } 
    }

    internal class FakeQueueBindingProvider : IBindingProvider
    {
        FakeQueueClient _client;
        IConverterManager _converterManager;

        public FakeQueueBindingProvider(FakeQueueClient client, IConverterManager converterManager)
        {
            _client = client;
            _converterManager = converterManager;
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            ParameterInfo parameter = context.Parameter;
            string resolvedName = "fakequeue";
            Func<string, FakeQueueClient> invokeStringBinder = (invokeString) => _client;

            IBinding binding = GenericBinder.BindCollector<FakeQueueData, FakeQueueClient>(
                parameter,
                _converterManager,
                _client,
                (client, valueBindingContext) => client,
                resolvedName,
                invokeStringBinder
            );

            return Task.FromResult(binding);
        }
    }
}