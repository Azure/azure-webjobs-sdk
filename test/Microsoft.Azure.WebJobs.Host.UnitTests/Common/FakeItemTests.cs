﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System;
using Xunit;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Host.UnitTests.Indexers;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    // Tests for the BindToGenericItem rule. 
    public class FakeItemAttribute : Attribute
    {
        [AutoResolve]
        public string Index { get; set; }
    }

    public class FakeItemTests
    {

        public class Item
        {
            public int value;
        }

        public class Functions
        {
            public void F1([FakeItem(Index = "abc")] Item x)
            {
                x.value++;
            }
        }

        [Fact]
        public void Test()
        {
            var nr = new DictNameResolver();
            nr.Add("appsetting1", "val1");
            JobHostConfiguration config = new JobHostConfiguration()
            {
                TypeLocator = new FakeTypeLocator(typeof(Functions)),
                NameResolver = nr
            };

            var client = new FakeItemClient();
            client._dict["abc"] = new Item
            {
                value = 123
            };
            IExtensionRegistry extensions = config.GetService<IExtensionRegistry>();
            extensions.RegisterExtension<IExtensionConfigProvider>(client);

            JobHost host = new JobHost(config);

            var method = typeof(Functions).GetMethod("F1");
            host.Call(method);

            var item = (Item)client._dict["abc"];
            Assert.Equal(124, item.value);
        }

    }

    public class FakeItemClient : IExtensionConfigProvider
    {
        public Dictionary<string, object> _dict = new Dictionary<string, object>();

        void IExtensionConfigProvider.Initialize(ExtensionConfigContext context)
        {
            IExtensionRegistry extensions = context.Config.GetService<IExtensionRegistry>();
            var bf = context.Config.BindingFactory;

            var rule = bf.BindToGenericItem<FakeItemAttribute>(BuildFromAttribute);

            extensions.RegisterBindingRules<FakeItemAttribute>(rule);
        }

        private Task<IValueBinder> BuildFromAttribute(FakeItemAttribute attr, Type parameterType)
        {
            var type = typeof(MySpecialValueBinder<>).MakeGenericType(parameterType);
            var result = (IValueBinder) Activator.CreateInstance(type, this, attr.Index);
            return Task.FromResult<IValueBinder>(result);
        }

        public class MySpecialValueBinder<T> : IValueBinder
        {
            private FakeItemClient _client;
            private string _index;

            public MySpecialValueBinder(FakeItemClient arg, string index)
            {
                _client = arg;
                _index = index;
            }

            public Type Type
            {
                get
                {
                    return typeof(T);
                }
            }

            public object GetValue()
            {
                // Clone to mimic real network semantics - we're not sharing in-memory objects. 
                var obj =  _client._dict[_index];
                string json = JsonConvert.SerializeObject(obj);
                var clone = JsonConvert.DeserializeObject(json, this.Type);
                return clone;
            }

            public Task SetValueAsync(object value, CancellationToken cancellationToken)
            {
                _client._dict[_index] = value;
                return Task.FromResult(0);
            }

            public string ToInvokeString()
            {
                throw new NotImplementedException();
            }
        }
    }
}