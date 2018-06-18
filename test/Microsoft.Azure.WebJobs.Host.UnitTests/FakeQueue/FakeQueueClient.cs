// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    // For sending fake queue messages. 
    public class FakeQueueClient : IExtensionConfigProvider, IConverter<FakeQueueAttribute, FakeQueueClient>
    {
        private readonly INameResolver _nameResolver;
        private readonly IConverterManager _converterManager;

        public FakeQueueClient(INameResolver nameResolver, IConverterManager converterManager)
        {
            _nameResolver = nameResolver;
            _converterManager = converterManager;
        }

        public List<FakeQueueData> _items = new List<FakeQueueData>();

        public Dictionary<string, List<FakeQueueData>> _prefixedItems = new Dictionary<string, List<FakeQueueData>>();

        public Task AddAsync(FakeQueueData item, CancellationToken cancellationToken = default(CancellationToken))
        {
            _items.Add(item);
            return Task.FromResult(0);
        }

        public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            // Batching not supported. 
            return Task.FromResult(0);
        }

        // Test hook for customizing converters
        public Action<ExtensionConfigContext> SetConverters
        {
            get; set;
        }

        void IExtensionConfigProvider.Initialize(ExtensionConfigContext context)
        {
            var rule = context.AddBindingRule<FakeQueueAttribute>();

            context.AddConverter<string, FakeQueueData>(x => new FakeQueueData { Message = x });
            context.AddConverter<FakeQueueData, string>(msg => msg.Message);
            context.AddConverter<OtherFakeQueueData, FakeQueueData>(OtherFakeQueueData.ToEvent);

            rule.AddOpenConverter<OpenType.Poco, FakeQueueData>(ConvertPocoToFakeQueueMessage);

            SetConverters?.Invoke(context);

            // Binds [FakeQueue] --> IAsyncCollector<FakeQueueData>
            rule.BindToCollector<FakeQueueData>(BuildFromAttr);

            // Binds [FakeQueue] --> FakeQueueClient
            rule.BindToInput<FakeQueueClient>(this);

            var triggerBindingProvider = new FakeQueueTriggerBindingProvider(this, _converterManager);
            context.AddBindingRule<FakeQueueTriggerAttribute>()
                .BindToTrigger(triggerBindingProvider);
        }

        private Task<object> ConvertFakeQueueMessageToPoco(object src, Attribute attribute, ValueBindingContext context)
        {
            throw new NotImplementedException();
        }

        private Task<object> ConvertPocoToFakeQueueMessage(object arg, Attribute attrResolved, ValueBindingContext context)
        {
            return Task.FromResult<object>(new FakeQueueData { Message = JObject.FromObject(arg).ToString() });
        }

        FakeQueueClient IConverter<FakeQueueAttribute, FakeQueueClient>.Convert(FakeQueueAttribute attr)
        {
            // Ensure that you can access the state set by the custom IResolutionPolicy
            Assert.Equal("value1", attr.State1);
            Assert.Equal("value2", attr.State2);

            return this;
        }

        private IAsyncCollector<FakeQueueData> BuildFromAttr(FakeQueueAttribute attr)
        {
            // Caller already resolved anything. 
            return new Myqueue
            {
                _parent = this,
                _prefix = attr.Prefix
            };
        }

        public IAsyncCollector<FakeQueueData> GetQueue()
        {
            return new Myqueue
            {
                _parent = this
            };
        }

        class Myqueue : IAsyncCollector<FakeQueueData>
        {
            internal FakeQueueClient _parent;
            internal string _prefix;

            public async Task AddAsync(FakeQueueData item, CancellationToken cancellationToken = default(CancellationToken))
            {
                if (_prefix != null)
                {
                    // Add these to a look-aside buffer. Won't trigger further  
                    item.ExtraPropertery = _prefix;
                    List<FakeQueueData> l;
                    if (!_parent._prefixedItems.TryGetValue(_prefix, out l))
                    {
                        l = new List<FakeQueueData>();
                        _parent._prefixedItems[_prefix] = l;
                    }
                    l.Add(item);
                }
                else
                {
                    await _parent.AddAsync(item);
                }
            }

            public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                return _parent.FlushAsync();
            }
        }
    }

    // A Test class that's not related to FakeQueueData, but can be converted to/from it. 
    public class OtherFakeQueueData
    {
        public string _test;

        public static FakeQueueData ToEvent(OtherFakeQueueData x)
        {
            return new FakeQueueData
            {
                ExtraPropertery = x._test
            };
        }
    }
}