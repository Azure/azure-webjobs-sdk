// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

namespace Microsoft.Azure.WebJobs.Logging
{
    internal class DictionaryLoggerScope : IDisposable
    {
        private static AsyncLocal<DictionaryLoggerScope> _value = new AsyncLocal<DictionaryLoggerScope>();

        private DictionaryLoggerScope(IEnumerable<KeyValuePair<string, object>> state, DictionaryLoggerScope parent)
        {
            _state = state;
            Parent = parent;
        }

        private readonly IEnumerable<KeyValuePair<string, object>> _state;
        private Dictionary<string, object> _cached;

        private static readonly Dictionary<string, object> Empty = new Dictionary<string, object>();

        internal DictionaryLoggerScope Parent { get; private set; }

        public static DictionaryLoggerScope Current
        {
            get
            {
                return _value.Value;
            }

            set
            {
                _value.Value = value;
            }
        }

        public static IDisposable Push(object state)
        {
            return Current = new DictionaryLoggerScope(state as IEnumerable<KeyValuePair<string, object>>, Current);
        }

        public static IDisposable Push(object inner, object outer)
        {
            var innerEnumerable = inner as IEnumerable<KeyValuePair<string, object>>;
            var outerEnumerable = outer as IEnumerable<KeyValuePair<string, object>>;

            IEnumerable<KeyValuePair<string, object>> enumerable;

            if (innerEnumerable == null)
            {
                enumerable = outerEnumerable;
            }
            else if (outerEnumerable == null)
            {
                enumerable = innerEnumerable;
            }
            else
            {
                enumerable = innerEnumerable.Concat(outerEnumerable);
            }

            return Current = new DictionaryLoggerScope(enumerable, Current);
        }

        // Builds a state dictionary of all scopes. If an inner scope
        // contains the same key as an outer scope, it overwrites the value.
        public static IReadOnlyDictionary<string, object> GetMergedStateDictionary()
        {
            var current = Current;
            if (current == null)
            {
                // no scope, return empty
                return Empty;
            }

            if (current._cached != null)
            {
                // already cached, return cached
                return current._cached;
            }
            
            var cached = current._cached = new Dictionary<string, object>();

            while (current != null)
            {
                if (current._state != null)
                {
                    foreach (var entry in current._state)
                    {
                        // inner scopes win
                        if (!cached.ContainsKey(entry.Key))
                        {
                            cached.Add(entry.Key, entry.Value);
                        }
                    }
                }
                current = current.Parent;
            }

            return cached;
        }

        public void Dispose()
        {
            Current = Current.Parent;
        }
    }
}