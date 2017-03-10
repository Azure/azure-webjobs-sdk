// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;

namespace Microsoft.Azure.WebJobs.Host.Loggers
{
    internal class AppInsightsScope
    {
        private readonly object _state;

        private AppInsightsScope(object state, AppInsightsScope parent)
        {
            _state = state;
            Parent = parent;
        }

        internal AppInsightsScope Parent { get; private set; }

        private static AsyncLocal<AppInsightsScope> _value = new AsyncLocal<AppInsightsScope>();
        public static AppInsightsScope Current
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
            Current = new AppInsightsScope(state, Current);
            return new DisposableScope();
        }

        // Builds a state dictionary of all scopes. If an inner scope
        // contains the same key as an outer scope, it overwrites the value.
        public IDictionary<string, object> GetMergedStateDictionary()
        {
            IDictionary<string, object> scopeInfo = new Dictionary<string, object>();

            var current = Current;
            while (current != null)
            {
                foreach (var entry in current.GetStateDictionary())
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

        private IDictionary<string, object> GetStateDictionary()
        {
            return _state as IDictionary<string, object>;
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