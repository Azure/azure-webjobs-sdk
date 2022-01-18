// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    internal struct ScopeProperties : IDisposable, IEnumerable<KeyValuePair<string, object>>
    {
        private static readonly AsyncLocal<ScopePropertyOrBreak> _current = new AsyncLocal<ScopePropertyOrBreak>();

        public ScopeProperties() => _current.Value = ScopePropertyOrBreak.Break(_current.Value);

        public void Add(string name, object value)
        {
            var lastCurrent = _current.Value;
            _current.Value = new ScopePropertyOrBreak(lastCurrent, name, value);
        }

        public void Add(KeyValuePair<string, object> pair)
        {
            var lastCurrent = _current.Value;
            _current.Value = new ScopePropertyOrBreak(lastCurrent, pair);
        }

        /// <summary>
        /// Throw away this scope, by rolling back up past the last break.
        /// </summary>
        public void Dispose()
        {
            var cursor = _current.Value;
            while (cursor != null)
            {
                if (cursor.IsBreak)
                {
                    _current.Value = cursor.Parent;
                    break;
                }
                cursor = cursor.Parent;
            }
        }

        public static bool TryGetValue<T>(string key, out T result)
        {
            if (TryGetValue(key, out object objResult))
            {
                try
                {
                    result = (T)objResult;
                    return true;
                }
                catch
                {
                    // Ignore, we didn't succeed
                }
            }
            result = default;
            return false;
        }

        public static bool TryGetValue(string key, out object result)
        {
            var cursor = _current.Value;
            while (cursor != null)
            {
                if (cursor.Pair.Key == key)
                {
                    result = cursor.Pair.Value;
                    return true;
                }
                cursor = cursor.Parent;
            }

            result = default;
            return false;
        }

        public static bool ContainsKey(string key) => TryGetValue(key, out _);

        public static IEnumerable<KeyValuePair<string, object>> GetAll()
        {
            var cursor = _current.Value;
            while (cursor != null)
            {
                if (!cursor.IsBreak)
                {
                    yield return cursor.Pair;
                }
                cursor = cursor.Parent;
            }
        }

        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator() => new ScopeEnumerator(_current.Value);
        IEnumerator IEnumerable.GetEnumerator() => new ScopeEnumerator(_current.Value);

        public struct ScopeEnumerator : IEnumerator<KeyValuePair<string, object>>
        {
            private ScopePropertyOrBreak _nextNode;
            private ScopePropertyOrBreak _currentItem;

            public ScopeEnumerator(ScopePropertyOrBreak head)
            {
                _nextNode = head;
                _currentItem = default;
            }

            public KeyValuePair<string, object> Current => _currentItem?.Pair ?? default;

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                // Skip scope breaks upward
                while (_nextNode?.IsBreak == true)
                {
                    _nextNode = _nextNode.Parent;
                }

                if (_nextNode == null)
                {
                    _currentItem = default;
                    return false;
                }

                _currentItem = _nextNode;
                _nextNode = _nextNode.Parent;

                return true;
            }

            public void Reset() => throw new NotSupportedException();
            public void Dispose() { }
        }

        public class ScopePropertyOrBreak
        {
            public ScopePropertyOrBreak Parent { get; }
            public KeyValuePair<string, object> Pair { get; }
            public bool IsBreak { get; } // Used as a marker between scopes to maintain previous behavior

            public ScopePropertyOrBreak(ScopePropertyOrBreak parent, string name, object value) => (Parent, Pair) = (parent, new KeyValuePair<string, object>(name, value));
            public ScopePropertyOrBreak(ScopePropertyOrBreak parent, KeyValuePair<string, object> pair) => (Parent, Pair) = (parent, pair);
            private ScopePropertyOrBreak(ScopePropertyOrBreak parent) => (Parent, IsBreak) = (parent, true);

            public static ScopePropertyOrBreak Break(ScopePropertyOrBreak parent) => new ScopePropertyOrBreak(parent);
        }
    }
}