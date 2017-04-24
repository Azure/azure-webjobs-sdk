// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    /// <summary>
    ///  Context passed to <see cref="AutoResolveAttribute"/> to aide in resolution. 
    /// </summary>
    internal class ClonerContext
    {
        public string MethodName { get; set; }

        public INameResolver NameResolver { get; set; }

        public static ClonerContext New(INameResolver resolver)
        {
            return new ClonerContext
            {
                NameResolver = resolver,
            };
        }

        public static ClonerContext New(INameResolver resolver, ParameterInfo parameter)
        {
            return new ClonerContext
            {
                 NameResolver = resolver,
                 MethodName = parameter.Member.Name
            };
        }
        public INameResolver GetResolverOrDefault()
        {
            return NameResolver ?? new EmptyNameResolver();
        }

        // If no name resolver is specified, then any %% becomes an error.
        private class EmptyNameResolver : INameResolver
        {
            public string Resolve(string name) => null;
        }
    }
}
