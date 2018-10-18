// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Host
{
    internal class DefaultExtensionRegistry : IExtensionRegistry
    {
        private readonly object _lock = new object();
        private ConcurrentDictionary<Type, ICollection<object>> _registry = new ConcurrentDictionary<Type, ICollection<object>>();

        public void RegisterExtension(Type type, object instance)
        {
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }
            if (instance == null)
            {
                throw new ArgumentNullException("instance");
            }
            if (!type.IsAssignableFrom(instance.GetType()))
            {
                throw new ArgumentOutOfRangeException("instance");
            }

            ICollection<object> instances = _registry.GetOrAdd(type, (t) => new Collection<object>());
            lock (_lock)
            {
                instances.Add(instance);
            }
        }

        public IEnumerable<object> GetExtensions(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }

            if (_registry.TryGetValue(type, out ICollection<object> instances))
            {
                lock (_lock)
                {
                    return instances.ToArray();
                }
            }

            return Enumerable.Empty<object>();
        }
    }
}
