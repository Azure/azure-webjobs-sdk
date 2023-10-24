// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Logging
{
    internal readonly struct FunctionResultAggregateState : IReadOnlyList<KeyValuePair<string, object>>
    {
        public const string OriginalFormatString = $"Aggregate result (function={{{LogConstants.NameKey}}}, count={{{LogConstants.CountKey}}}, time={{{LogConstants.TimestampKey}}}, duration={{{LogConstants.AverageDurationKey}}}ms, maxDuration={{{LogConstants.MaxDurationKey}}}ms, minDuration={{{LogConstants.MinDurationKey}}}ms, success={{{LogConstants.SuccessesKey}}}, failure={{{LogConstants.FailuresKey}}}, rate={{{LogConstants.SuccessRateKey}}})";
        private readonly FunctionResultAggregate _resultAggregate;

        public FunctionResultAggregateState(FunctionResultAggregate resultAggregate)
        {
            _resultAggregate = resultAggregate;
        }

        public KeyValuePair<string, object> this[int index]
        {
            get
            {
                return index switch
                {
                    0 => new KeyValuePair<string, object>(LogConstants.NameKey, _resultAggregate.Name),
                    1 => new KeyValuePair<string, object>(LogConstants.CountKey, _resultAggregate.Count),
                    2 => new KeyValuePair<string, object>(LogConstants.TimestampKey, _resultAggregate.Timestamp),
                    3 => new KeyValuePair<string, object>(LogConstants.AverageDurationKey, _resultAggregate.AverageDuration),
                    4 => new KeyValuePair<string, object>(LogConstants.MaxDurationKey, _resultAggregate.MaxDuration),
                    5 => new KeyValuePair<string, object>(LogConstants.MinDurationKey, _resultAggregate.MinDuration),
                    6 => new KeyValuePair<string, object>(LogConstants.SuccessesKey, _resultAggregate.Successes),
                    7 => new KeyValuePair<string, object>(LogConstants.FailuresKey, _resultAggregate.Failures),
                    8 => new KeyValuePair<string, object>(LogConstants.SuccessRateKey, _resultAggregate.SuccessRate),
                    9 => new KeyValuePair<string, object>(LogConstants.OriginalFormatKey, OriginalFormatString),
                    _ => throw new ArgumentOutOfRangeException(nameof(index)),
                };
            }
        }

        public int Count => 10;

        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
            => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        public override string ToString()
        {
            return $"Aggregate result (function={_resultAggregate.Name}, count={_resultAggregate.Count}, time={_resultAggregate.Timestamp}, duration={_resultAggregate.AverageDuration}ms, maxDuration={_resultAggregate.MaxDuration}ms, minDuration={_resultAggregate.MinDuration}ms, success={_resultAggregate.Successes}, failure={_resultAggregate.Failures}, rate={_resultAggregate.SuccessRate})";
        }

        public struct Enumerator : IEnumerator<KeyValuePair<string, object>>
        {
            private readonly FunctionResultAggregateState _state;
            private int _index = -1;

            public Enumerator(FunctionResultAggregateState state)
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
