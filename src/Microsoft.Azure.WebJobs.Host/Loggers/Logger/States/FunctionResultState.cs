// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Loggers;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Logging
{
    internal readonly struct FunctionResultState : IReadOnlyList<KeyValuePair<string, object>>
    {
        public const string OriginalFormatString = $"Result '{{{LogConstants.NameKey}}}' (started at={{{LogConstants.StartTimeKey}}}, duration={{{LogConstants.DurationKey}}}, succeeded={{{LogConstants.SucceededKey}}})";
        private readonly FunctionInstanceLogEntry _logEntry;
        private readonly bool _succeeded;

        public FunctionResultState(FunctionInstanceLogEntry logEntry, bool succeeded)
        {
            _logEntry = logEntry;
            _succeeded = succeeded;
        }

        public KeyValuePair<string, object> this[int index]
        {
            get
            {
                return index switch
                {
                    0 => new KeyValuePair<string, object>(LogConstants.FullNameKey, _logEntry.FunctionName),
                    1 => new KeyValuePair<string, object>(LogConstants.InvocationIdKey, _logEntry.FunctionInstanceId),
                    2 => new KeyValuePair<string, object>(LogConstants.NameKey, _logEntry.LogName),
                    3 => new KeyValuePair<string, object>(LogConstants.TriggerReasonKey, _logEntry.TriggerReason),
                    4 => new KeyValuePair<string, object>(LogConstants.StartTimeKey, _logEntry.StartTime),
                    5 => new KeyValuePair<string, object>(LogConstants.EndTimeKey, _logEntry.EndTime),
                    6 => new KeyValuePair<string, object>(LogConstants.DurationKey, _logEntry.Duration),
                    7 => new KeyValuePair<string, object>(LogConstants.SucceededKey, _succeeded),
                    8 => new KeyValuePair<string, object>(LogConstants.OriginalFormatKey, OriginalFormatString),
                    _ => throw new ArgumentOutOfRangeException(nameof(index)),
                };
            }
        }

        public int Count => 9;

        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
            => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        public override string ToString()
        {
            return $"Result '{_logEntry.FunctionName}' (started at={_logEntry.StartTime}, duration={_logEntry.Duration}, succeeded={_succeeded})";
        }

        public struct Enumerator : IEnumerator<KeyValuePair<string, object>>
        {
            private readonly FunctionResultState _state;
            private int _index = -1;

            public Enumerator(FunctionResultState state)
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
