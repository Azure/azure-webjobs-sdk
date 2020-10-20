// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// The base class for all readonly dictionaries used for scopes
    /// </summary>
    public class ReadOnlyScopeDictionary : IReadOnlyDictionary<string, object>
    {
        readonly string[] _keys;
        readonly object[] _values;

        public ReadOnlyScopeDictionary(string[] keys, object[] values)
        {
            _keys = keys;
            _values = values;
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Count => _keys.Length;

        public bool ContainsKey(string key)
        {
            for (var i = 0; i < _keys.Length; i++)
            {
                if (_keys[i] == key)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetValue(string key, out object value)
        {
            for (var i = 0; i < _keys.Length; i++)
            {
                if (_keys[i] == key)
                {
                    value = _values[i];
                    return true;
                }
            }

            value = null;
            return false;
        }

        public object this[string key]
        {
            get
            {
                if (!TryGetValue(key, out object value))
                {
                    throw new KeyNotFoundException();
                }

                return value;
            }
        }

        public IEnumerable<string> Keys => _keys;
        public IEnumerable<object> Values => _values;

        class Enumerator : IEnumerator<KeyValuePair<string, object>>
        {
            private readonly ReadOnlyScopeDictionary _dictionary;
            private int _index;

            public Enumerator(ReadOnlyScopeDictionary dictionary)
            {
                _dictionary = dictionary;
                _index = -1;
            }

            public bool MoveNext()
            {
                _index += 1;
                if (_index >= _dictionary._keys.Length)
                {
                    return false;
                }
                return true;
            }

            public void Reset() { }

            public KeyValuePair<string, object> Current => new KeyValuePair<string, object>(_dictionary._keys[_index], _dictionary._values[_index]);

            object IEnumerator.Current => Current;

            public void Dispose() { }
        }
    }
}