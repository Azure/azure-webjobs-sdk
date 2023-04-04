// Copyright (c) .NET Foundation. All rights reserved.
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

        // Cache merged dictionary.  
        internal IReadOnlyDictionary<string, object> CurrentScope { get; private set; }

        internal DictionaryLoggerScope Parent { get; private set; }

        private DictionaryLoggerScope(IReadOnlyDictionary<string, object> currentScope, DictionaryLoggerScope parent)
        {
            CurrentScope = currentScope;
            Parent = parent;
        }

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
            if (state is IDictionary<string, object> currentState)
            {
                BuildCurrentScope(currentState);
            }
            else if (state is IEnumerable<KeyValuePair<string, object>> stateEnum)
            {
                IDictionary<string, object> stateValues;
                // Convert this to a dictionary as we have scenarios where we cannot have duplicates.
                // In this case, if there are dupes, the later entry wins.
                stateValues = new Dictionary<string, object>();
                foreach (var entry in stateEnum)
                {
                    stateValues[entry.Key] = entry.Value;
                }
                BuildCurrentScope(stateValues);
            }
            else
            {
                // There's nothing we can do with other states.
                return null;
            }
            return new DisposableScope();
        }

        private static void BuildCurrentScope(IDictionary<string, object> state)
        {
            IDictionary<string, object> scopeInfo;

            // Copy the current scope to the new scope dictionary
            if (Current != null && Current.CurrentScope != null)
            {
                scopeInfo = new Dictionary<string, object>(state.Count + Current.CurrentScope.Count, StringComparer.Ordinal);

                foreach (var entry in Current.CurrentScope)
                {
                    scopeInfo.Add(entry);
                }
                // If the state contains the same key as current scope, it overwrites the value.
                foreach (var entry in state)
                {
                    scopeInfo[entry.Key] = entry.Value;
                }
            }
            else
            {
                scopeInfo = new Dictionary<string, object>(state, StringComparer.Ordinal);
            }
            Current = new DictionaryLoggerScope(new ReadOnlyDictionary<string, object>(scopeInfo), Current);
        }
                
        public static IReadOnlyDictionary<string, object> GetMergedStateDictionaryOrNull()
        {
            if (Current == null)
            {
                return null;
            }
            return Current.CurrentScope;
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