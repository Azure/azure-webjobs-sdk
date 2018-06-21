﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Indexers
{
    public class NameResolverTests
    {
        [Fact]
        public static void ResolveNameSet()
        {
            INameResolver resolver = new MockNameResolver { OnResolve = (name) => name.ToUpper() };

            Assert.Equal("ABC", resolver.ResolveWholeString("%abc%"));
            Assert.Equal("1ABC23XYZ4", resolver.ResolveWholeString("1%abc%23%xyz%4"));
            Assert.Equal("ABCdefXYZ", resolver.ResolveWholeString("%abc%def%xyz%"));
            Assert.Equal("ab", resolver.ResolveWholeString("a%%b"));

            Assert.Throws<InvalidOperationException>( () => resolver.ResolveWholeString("%abc")); // no closing %
        }

        [Fact]
        public static void NotRecursive()
        {
            var resolver = new FakeNameResolver();            
            resolver.Add("one", "1");
            resolver.Add("two", "2");
            resolver.Add("<", "%");
            resolver.Add(">", "%");            

            Assert.Equal("1", resolver.ResolveWholeString("%one%"));
            Assert.Equal("12", resolver.ResolveWholeString("%one%%two%")); // directly adjacent

            Assert.Equal("%one%", resolver.ResolveWholeString("%<%one%>%")); // Not recurisve, only applied once.

            // Failure when resolving a missing item 
            Assert.Throws<InvalidOperationException>( ()=> resolver.ResolveWholeString("%one%_%missing%"));
        }

        [Fact]
        public static void ResolveNameNotSet()
        {
            INameResolver resolver = null;
            ExceptionAssert.ThrowsArgumentNull(() => resolver.ResolveWholeString("1%abc%23%xyz%4"), "resolver");
        }

        [Fact]
        public static void ResolverThrowIsWrapped()
        {
            INameResolver resolver = new MockNameResolver { OnResolve = (name) => { throw new NotImplementedException(); } };

            Assert.Equal("abc", resolver.ResolveWholeString("abc")); // never called, ok

            // rogue exceptions are caught and wrapped. 
            Assert.Throws<InvalidOperationException>(() => resolver.ResolveWholeString("%abc%"));
        }
    }

    class MockNameResolver : INameResolver
    {
        public Func<string, string> OnResolve;

        public string Resolve(string name)
        {
            return OnResolve(name);
        }
    }
}
