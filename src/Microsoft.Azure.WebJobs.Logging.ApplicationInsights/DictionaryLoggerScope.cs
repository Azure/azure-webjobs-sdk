﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace Microsoft.Azure.WebJobs.Logging
{
    internal class DictionaryLoggerScope
    {
        private static AsyncLocal<DictionaryLoggerScope> _value = new AsyncLocal<DictionaryLoggerScope>();

        private DictionaryLoggerScope(IReadOnlyDictionary<string, object> state, DictionaryLoggerScope parent)
        {
            State = state;
            Parent = parent;
        }

        internal IReadOnlyDictionary<string, object> State { get; private set; }

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
            if (state is IEnumerable<KeyValuePair<string, object>> stateEnum)
            {
                // Convert this to a dictionary as we have scenarios where we cannot have duplicates. In this
                // case, if there are dupes, the later entry wins.
                var stateValues = new Dictionary<string, object>();
                foreach (var entry in stateEnum)
                {
                    stateValues[entry.Key] = entry.Value;
                }
                Current = new DictionaryLoggerScope(new ReadOnlyDictionary<string, object>(stateValues), Current);
                return new DisposableScope();
            }

            // There's nothing we can do with other states.
            return null;
        }


        // Builds a state dictionary of all scopes. If an inner scope
        // contains the same key as an outer scope, it overwrites the value.
        public static IEnumerable<KeyValuePair<string, object>> GetMergedIterator()
        {
            var current = Current;
            var seen = new HashSet<string>();
            while (current != null)
            {
                foreach (var entry in current.State)
                {
                    // To maintain previous behavior, see if it's a true from .Add (new key we haven't seen yet)
                    // If not, don't yield it out
                    if (seen.Add(entry.Key))
                    {
                        yield return entry;
                    }
                }
                current = current.Parent;
            }
        }

        public static bool Any()
        {
            var current = Current;
            while (current != null)
            {
                if (current.State?.Count > 0)
                {
                    return true;
                }
                current = current.Parent;
            }
            return false;
        }

        public static bool ContainsKey(string key)
        {
            var current = Current;
            while (current != null)
            {
                if (current.State.ContainsKey(key))
                {
                    return true;
                }
                current = current.Parent;
            }
            return false;
        }

        public static T GetValueOrDefault<T>(string key)
        {
            var current = Current;
            while (current != null)
            {
                if (current.State.TryGetValue(key, out var obj))
                {
                    return (T)obj;
                }
                current = current.Parent;
            }
            return default;
        }

        public static bool TryGetValue(string key, out object result)
        {
            var current = Current;
            while (current != null)
            {
                if (current.State.TryGetValue(key, out result))
                {
                    return true;
                }
                current = current.Parent;
            }
            result = null;
            return false;
        }

        // Builds a state dictionary of all scopes. If an inner scope
        // contains the same key as an outer scope, it overwrites the value.
        public static IDictionary<string, object> GetMergedStateDictionaryOrNull()
        {
            IDictionary<string, object> scopeInfo = null;

            var current = Current;
            while (current != null)
            {
                if (scopeInfo == null)
                {
                    scopeInfo = new Dictionary<string, object>();
                }

                foreach (var entry in current.State)
                {
                    // inner scopes win
                    if (!scopeInfo.Keys.Contains(entry.Key))
                    {
                        scopeInfo.Add(entry);
                    }
                }
                current = current.Parent;
            }

            return scopeInfo;
        }

        private class DisposableScope : IDisposable
        {
            public void Dispose()
            {
                Current = Current.Parent;
            }
        }
    }
}