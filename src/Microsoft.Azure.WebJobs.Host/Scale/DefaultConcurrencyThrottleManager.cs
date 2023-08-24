// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    internal class DefaultConcurrencyThrottleManager : IConcurrencyThrottleManager
    {
        private readonly TimeSpan _minUpdateInterval = TimeSpan.FromMilliseconds(1000);
        private readonly IEnumerable<IConcurrencyThrottleProvider> _throttleProviders;
        private readonly ILogger _logger;

        private object _syncLock = new object();
        private ThrottleState _throttleState;
        private List<string>? _enabledThrottles;
        private int _consecutiveCount;

        public DefaultConcurrencyThrottleManager(IEnumerable<IConcurrencyThrottleProvider> throttleProviders, ILoggerFactory loggerFactory)
        {
            if (throttleProviders == null)
            {
                throw new ArgumentNullException(nameof(throttleProviders));
            }
            _throttleProviders = throttleProviders.ToList();

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }
            _logger = loggerFactory.CreateLogger(LogCategories.Concurrency);

            LastThrottleCheckStopwatch = Stopwatch.StartNew();
        }

        internal Stopwatch LastThrottleCheckStopwatch { get; }

        public ConcurrencyThrottleAggregateStatus GetStatus()
        {
            // throttle querying of throttle providers so we're not calling them too often
            if (LastThrottleCheckStopwatch.Elapsed > _minUpdateInterval)
            {
                UpdateThrottleState();
            }

            ConcurrencyThrottleAggregateStatus status;
            lock (_syncLock)
            {
                status = new ConcurrencyThrottleAggregateStatus
                {
                    State = _throttleState,
                    EnabledThrottles = _enabledThrottles?.AsReadOnly(),
                    ConsecutiveCount = _consecutiveCount
                };
            }

            return status;
        }

        private void UpdateThrottleState()
        {
            IEnumerable<ConcurrencyThrottleStatus> throttleResults = _throttleProviders.Select(p => p.GetStatus(_logger)).ToArray();

            bool throttleEnabled = throttleResults.Any(p => p.State == ThrottleState.Enabled);
            ThrottleState newThrottleState;
            if (throttleEnabled)
            {
                // if any throttles are enabled, we're in an enabled state
                newThrottleState = ThrottleState.Enabled;
            }
            else if (throttleResults.Any(p => p.State == ThrottleState.Unknown))
            {
                // if no throttles are enabled, but at least 1 is in an unknown state
                // we're in an unknown state
                newThrottleState = ThrottleState.Unknown;
            }
            else
            {
                // all throttles are disabled
                newThrottleState = ThrottleState.Disabled;
            }

            List<string>? newEnabledThrottles = null;
            if (newThrottleState == ThrottleState.Enabled)
            {
                newEnabledThrottles = throttleResults.Where(p => p.EnabledThrottles != null).SelectMany(p => p.EnabledThrottles).Distinct().ToList();
            }

            lock (_syncLock)
            {
                if (newThrottleState == _throttleState)
                {
                    // throttle state has remained the same since the last time we checked
                    // so we're in a run
                    _consecutiveCount++;
                }
                else
                {
                    // throttle state has changed, so any run has ended
                    _consecutiveCount = 0;
                }

                _throttleState = newThrottleState;
                _enabledThrottles = newEnabledThrottles;
            }

            LastThrottleCheckStopwatch.Restart();
        }
    }
}
