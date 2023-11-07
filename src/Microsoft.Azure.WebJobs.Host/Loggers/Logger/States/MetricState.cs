// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Logging
{
    internal readonly struct MetricState : IReadOnlyList<KeyValuePair<string, object>>
    {
        public const string OriginalFormatString = $"Metric {{{LogConstants.NameKey}}} {{{LogConstants.MetricValueKey}}}";
        private readonly string _name;
        private readonly double _value;
        private readonly IReadOnlyList<KeyValuePair<string, object>> _properties;

        public MetricState(string name, double value, IDictionary<string, object> properties)
        {
            _name = name;
            _value = value;
            _properties = properties?.ToList();
        }

        public KeyValuePair<string, object> this[int index]
        {
            get
            {
                if (index == 0)
                {
                    return new KeyValuePair<string, object>(LogConstants.NameKey, _name);
                }

                if (index == 1)
                {
                    return new KeyValuePair<string, object>(LogConstants.MetricValueKey, _value);
                }

                index -= 2;

                if (_properties != null)
                {
                    if (index < _properties.Count)
                    {
                        return _properties[index];
                    }

                    index -= _properties.Count;
                }

                if (index == 0)
                {
                    return new KeyValuePair<string, object>(LogConstants.OriginalFormatKey, OriginalFormatString);
                }

                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public int Count => 3 + (_properties?.Count ?? 0);

        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
            => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        public override string ToString()
        {
            return $"Metric {_name} {_value}";
        }

        public struct Enumerator : IEnumerator<KeyValuePair<string, object>>
        {
            private readonly MetricState _state;
            private int _index = -1;

            public Enumerator(MetricState state)
            {
                _state = state;
            }

            public readonly KeyValuePair<string, object> Current
                => _state[_index];

            readonly object IEnumerator.Current => Current;

            public readonly void Dispose()
            {
            }

            public bool MoveNext()
            {
                return ++_index < _state.Count;
            }

            public readonly void Reset()
            {
                throw new NotSupportedException();
            }
        }
    }
}
