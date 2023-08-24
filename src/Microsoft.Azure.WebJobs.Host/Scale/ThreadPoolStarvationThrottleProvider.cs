// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// This throttle provider monitors for thread starvation signals. For a healthy signal, it relies on its
    /// internal timer being run consistently. Thus it acts as a "canary in the coal mine" (https://en.wikipedia.org/wiki/Sentinel_species)
    /// for thread pool starvation situations.
    /// </summary>
    internal class ThreadPoolStarvationThrottleProvider : IConcurrencyThrottleProvider, IDisposable
    {
        internal const string ThreadPoolStarvationThrottleName = "ThreadPoolStarvation";

        private const int IntervalMS = 100;
        private const double FailureThreshold = 0.5;

        private readonly ReadOnlyCollection<string> _exceededThrottles;
        private readonly object _syncLock = new object();

        private bool _disposedValue;
        private Timer? _timer;
        private int _invocations;
        private DateTime _lastCheck;

        public ThreadPoolStarvationThrottleProvider()
        {
            _exceededThrottles = new List<string> { ThreadPoolStarvationThrottleName }.AsReadOnly();
        }

        public void OnTimer(object state)
        {
            lock (_syncLock)
            {
                _invocations++;
            }
        }

        private void EnsureTimerStarted()
        {
            if (_timer == null)
            {
                lock (_syncLock)
                {
                    if (_timer == null)
                    {
                        _timer = new Timer(OnTimer, null, 0, IntervalMS);
                        _lastCheck = DateTime.UtcNow;
                    }
                }
            }
        }

        public ConcurrencyThrottleStatus GetStatus(ILogger? logger = null)
        {
            // we only start the timer on demand, ensuring that if state isn't being queried,
            // we're not performing unnecessary background work
            EnsureTimerStarted();

            int missedCount;
            int expectedCount;

            lock (_syncLock)
            {
                // determine how many occurrences we expect to have had since
                // the last check
                TimeSpan duration = DateTime.UtcNow - _lastCheck;
                expectedCount = (int)Math.Floor(duration.TotalMilliseconds / IntervalMS);

                // calculate how many we missed
                missedCount = Math.Max(0, expectedCount - _invocations);

                _invocations = 0;
                _lastCheck = DateTime.UtcNow;
            }

            // if the number of missed occurrences is over threshold
            // we know things are unhealthy
            var status = new ConcurrencyThrottleStatus
            {
                State = ThrottleState.Disabled
            };
            int failureThreshold = (int)(expectedCount * FailureThreshold);
            if (expectedCount > 0 && missedCount > failureThreshold)
            {
                logger?.HostThreadStarvation();
                status.State = ThrottleState.Enabled;
                status.EnabledThrottles = _exceededThrottles;
            }

            return status;
        }

        // for testing only
        internal void ResetInvocations()
        {
            lock (_syncLock)
            {
                _invocations = 0;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _timer?.Dispose();
                }
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
