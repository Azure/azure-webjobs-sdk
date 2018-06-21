// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;
using Microsoft.Azure.WebJobs.Description;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    // Test Attribute Cloner with Storage attributes. 
    public class AttributeClonerTests
    {
        private static IReadOnlyDictionary<string, Type> emptyContract = new Dictionary<string, Type>();

        // Helper to easily generate a fixed binding contract.
        private static IReadOnlyDictionary<string, Type> GetBindingContract(params string[] names)
        {
            var d = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in names)
            {
                d[name] = typeof(string);
            }
            return d;
        }

        private static IReadOnlyDictionary<string, Type> GetBindingContract(Dictionary<string, object> values)
        {
            var d = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in values)
            {
                d[kv.Key] = typeof(string);
            }
            return d;
        }

        private static BindingContext GetCtx(IReadOnlyDictionary<string, object> values)
        {
            BindingContext ctx = new BindingContext(
                new ValueBindingContext(null, CancellationToken.None),
                values);
            return ctx;
        }

        // Test on an attribute that does NOT implement IAttributeInvokeDescriptor
        // Key parameter is on the ctor
        [Fact]
        public void InvokeStringBlobAttribute()
        {
            foreach (var attr in new BlobAttribute[] {
                new BlobAttribute("container/{name}"),
                new BlobAttribute("container/constant", FileAccess.ReadWrite),
                new BlobAttribute("container/{name}", FileAccess.Write)
            })
            {
                var cloner = new AttributeCloner<BlobAttribute>(attr, GetBindingContract("name"));
                BlobAttribute attr2 = cloner.ResolveFromInvokeString("c/n");

                Assert.Equal("c/n", attr2.BlobPath);
                Assert.Equal(attr.Access, attr2.Access);
            }
        }

        [Fact]
        public void CloneNoDefaultCtor()
        {
            var a1 = new BlobAttribute("container/{name}.txt", FileAccess.Write);

            Dictionary<string, object> values = new Dictionary<string, object>()
            {
                { "name", "green" },
            };
            var ctx = GetCtx(values);

            var cloner = new AttributeCloner<BlobAttribute>(a1, GetBindingContract("name"));
            var attr2 = cloner.ResolveFromBindingData(ctx);

            Assert.Equal("container/green.txt", attr2.BlobPath);
            Assert.Equal(a1.Access, attr2.Access);
        }

        [Fact]
        public void CloneNoDefaultCtorShortList()
        {
            // Use shorter parameter list.
            var a1 = new BlobAttribute("container/{name}.txt");

            Dictionary<string, object> values = new Dictionary<string, object>()
            {
                { "name", "green" },
            };
            var ctx = GetCtx(values);

            var cloner = new AttributeCloner<BlobAttribute>(a1, GetBindingContract("name"));
            var attr2 = cloner.ResolveFromBindingData(ctx);

            Assert.Equal("container/green.txt", attr2.BlobPath);
            Assert.Equal(a1.Access, attr2.Access);
        }
    }
}