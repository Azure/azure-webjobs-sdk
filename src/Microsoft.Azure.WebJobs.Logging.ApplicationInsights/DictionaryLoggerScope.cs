﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
        private static IDictionary<string, object> _currentScope;
        private static int _itemCount;
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
            if (state is IDictionary<string, object> currentState)
            {
                Current = new DictionaryLoggerScope(new ReadOnlyDictionary<string, object>(currentState), Current);
                _itemCount += currentState.Count;
            }
            else if (state is IEnumerable<KeyValuePair<string, object>> stateEnum)
            {
                IDictionary<string, object> stateValues;            
                // Convert this to a dictionary as we have scenarios where we cannot have duplicates. In this
                // case, if there are dupes, the later entry wins.
                stateValues = new Dictionary<string, object>();
                foreach (var entry in stateEnum)
                {
                    stateValues[entry.Key] = entry.Value;
                }
                _itemCount += stateValues.Count;
                Current = new DictionaryLoggerScope(new ReadOnlyDictionary<string, object>(stateValues), Current);
            }
            else
            {
                // There's nothing we can do with other states.
                return null;
            }            
            _currentScope = null;
            return new DisposableScope();
        }

        // Builds a state dictionary of all scopes. If an inner scope
        // contains the same key as an outer scope, it overwrites the value.
        public static IDictionary<string, object> GetMergedStateDictionaryOrNull()
        {
            if (Current == null)
            {
                return null;
            }
            if (_currentScope == null)
            {
                IDictionary<string, object> scopeInfo = new Dictionary<string, object>(_itemCount); 
                var current = Current;
                while (current != null)
                {
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
                _currentScope = scopeInfo;
                return scopeInfo;
            }
            else
            {
                return _currentScope;
            }
        }

        private class DisposableScope : IDisposable
        {
            public void Dispose()
            {
                _itemCount -= Current.State.Count;
                Current = Current.Parent;
                _currentScope = null;
            }
        }
    }
}