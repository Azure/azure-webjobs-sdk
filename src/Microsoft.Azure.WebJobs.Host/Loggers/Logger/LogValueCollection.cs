// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Internal;

namespace Microsoft.Azure.WebJobs.Host.Loggers
{
    internal class LogValueCollection : IReadOnlyList<KeyValuePair<string, object>>
    {
        private FormattedLogValues _formatter;
        private IReadOnlyList<KeyValuePair<string, object>> _additionalValues;

        public LogValueCollection(string format, object[] formatValues, IReadOnlyDictionary<string, object> additionalValues)
        {
            _formatter = new FormattedLogValues(format, formatValues);
            _additionalValues = additionalValues.ToList();
        }

        public int Count => _formatter.Count + _additionalValues.Count;

        public KeyValuePair<string, object> this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new IndexOutOfRangeException(nameof(index));
                }

                if (index < _additionalValues.Count)
                {
                    // if the index is lower, return the value from _additionalValues
                    return _additionalValues[index];
                }
                else
                {
                    // if there are no more additionalValues, return from _formatter
                    return _formatter[index - _additionalValues.Count];
                }
            }
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            for (int i = 0; i < Count; ++i)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            return _formatter.ToString();
        }
    }
}
