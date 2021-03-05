// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    public class ConcurrencyManager
    {
        private readonly IEnumerable<IHostThrottleProvider> _throttleProviders;

        private bool _dynamicConcurrencyEnabled;
        private int _maxDegreeOfParallelism;
        private ILogger _logger;
        private ConcurrentDictionary<string, ConcurrencyStatus> _concurrencyStatuses = new ConcurrentDictionary<string, ConcurrencyStatus>(StringComparer.OrdinalIgnoreCase);

        public ConcurrencyManager(ILogger<ConcurrencyManager> logger, IEnumerable<IHostThrottleProvider> throttleProviders)
        {
            // TODO: should this be constant for all functions, or taken as a param to Update?
            _maxDegreeOfParallelism = 100;
            
            _logger = logger;
            _throttleProviders = throttleProviders;
            _dynamicConcurrencyEnabled = true;
        }

        public bool Enabled => _dynamicConcurrencyEnabled;

        public void FunctionStarted(string functionId)
        {
            // TODO: we should be able to remove these explicit calls by integrating concurrency
            // manager with FunctionExecutor - it knows when invocations start/complete
            var concurrencyStatus = GetFunctionConcurrencyStatus(functionId);
            concurrencyStatus.FunctionStarted();
        }

        public void FunctionCompleted(string functionId)
        {
            var concurrencyStatus = GetFunctionConcurrencyStatus(functionId);
            concurrencyStatus.FunctionCompleted();
        }

        // update shouldn't be called concurrently for the same function
        // because it won't be, we can modify function specific concurrency settings
        // without locking
        public ConcurrencyStatus Update(string functionId)
        {
            var concurrencyStatus = UpdateCore(functionId);

            _logger.LogInformation($"Concurrency: {concurrencyStatus.CurrentParallelism}, OutstandingInvocations: {concurrencyStatus.OutstandingInvocations}");

            if (concurrencyStatus.ThrottleEnabled)
            {
                // if throttling is enabled, we can't take any work
                concurrencyStatus.FetchCount = 0;
            }
            else
            {
                // no throttles are enabled, so we can take work up to the current concurrency level
                concurrencyStatus.FetchCount = concurrencyStatus.CurrentParallelism - concurrencyStatus.OutstandingInvocations;
            }

            return concurrencyStatus;
        }

        private ConcurrencyStatus UpdateCore(string functionId)
        {
            var concurrencyStatus = GetFunctionConcurrencyStatus(functionId);

            // TODO: need to ensure that throttle providers are very efficient
            // Should we ping these on a timer internally, rather than on each update call?
            bool throttleEnabled = false;
            bool throttleUnknown = false;
            foreach (var provider in _throttleProviders)
            {
                // TODO: this logging is temporary - we can't be logging this for each individual
                // function. Instead the providers should log themselves periodically
                var status = provider.GetStatus(_logger);

                if (status.ThrottleState == ThrottleState.Enabled)
                {
                    throttleEnabled = true;
                }
                else if (status.ThrottleState == ThrottleState.Unknown)
                {
                    throttleUnknown = true;
                }
            }

            concurrencyStatus.ThrottleEnabled = throttleEnabled;
            if (throttleUnknown)
            {
                // if we're un an unknown state, we'll make no moves
                // however, we will take work at the current concurrency level
                return concurrencyStatus;
            }

            if (!concurrencyStatus.ThrottleEnabled)
            {
                // no throttles are enabled so host is healthy
                concurrencyStatus.ConsecutiveHealthyCount++;
                concurrencyStatus.ConsecutiveUnhealthyCount = 0;
            }
            else
            {
                // one or more throttles enabled, so host is unhealthy
                concurrencyStatus.ConsecutiveUnhealthyCount++;
                concurrencyStatus.ConsecutiveHealthyCount = 0;
            }

            int consecutiveResultLimit = 5;
            if (concurrencyStatus.ConsecutiveHealthyCount < consecutiveResultLimit && concurrencyStatus.ConsecutiveUnhealthyCount < consecutiveResultLimit)
            {
                // we only make a move one way or another if we've had consistent consecutive
                // results for a number of intervals
                return concurrencyStatus;
            }

            TimeSpan? timeSinceLastAdjustment = DateTime.UtcNow - concurrencyStatus.LastAdjustmentTimestamp;
            if (timeSinceLastAdjustment.Value.TotalSeconds < 5)
            {
                // don't adjust too often, either up or down
                // if we've made an adjustment recently, just return
                return concurrencyStatus;
            }

            // If we've had successful invocations, we can skip the check above and increase
            // more aggressively. We don't want to artificially throttle invocations that are suceeding
            // just fine.
            // 
            // TODO: adjust concurrency up in larger steps when healthy? E.g. by a percentage?
            // TODO: if we've been unhealthy for a while, should we reduce concurency?
            // ? how to prevent oscillation
            if (!concurrencyStatus.ThrottleEnabled)
            {
                if (concurrencyStatus.ExecutionsSinceLastConcurrencyAdjustment > 0 && concurrencyStatus.CurrentParallelism < _maxDegreeOfParallelism)
                {
                    // first check to see if we're attempting to move to a concurrency level that recently failed
                    timeSinceLastAdjustment = DateTime.UtcNow - concurrencyStatus.LastFailedAdjustmentTimestamp;
                    if (timeSinceLastAdjustment != null && timeSinceLastAdjustment.Value.TotalSeconds < 60)
                    {
                        if (concurrencyStatus.CurrentParallelism + 1 >= concurrencyStatus.LastFailedConcurrencyLevel)
                        {
                            return concurrencyStatus;
                        }
                    }
                    else
                    {
                        // after the interval expires, clear it
                        concurrencyStatus.LastFailedAdjustmentTimestamp = null;
                    }

                    if (concurrencyStatus.OutstandingInvocations >= concurrencyStatus.CurrentParallelism)
                    {
                        // ensure before we increment that we've actually executed
                        // up to the concurrency limit
                        // TODO: this might not work well for functions that complete quickly?
                        // Also, should we be tracking a high water mark, rather than checking the current #?
                        concurrencyStatus.CurrentParallelism++;
                        concurrencyStatus.LastAdjustmentTimestamp = DateTime.UtcNow;
                    }
                }
            }
            else if (concurrencyStatus.CurrentParallelism > 1)
            {
                // if we were unhealthy and we're adjusting down, keep track of the high water mark
                // so we don't attempt to scale back to that too quickly
                concurrencyStatus.LastFailedConcurrencyLevel = concurrencyStatus.CurrentParallelism;
                concurrencyStatus.LastFailedAdjustmentTimestamp = DateTime.UtcNow;

                concurrencyStatus.CurrentParallelism--;
                concurrencyStatus.LastAdjustmentTimestamp = DateTime.UtcNow;
            }

            concurrencyStatus.ExecutionsSinceLastConcurrencyAdjustment = 0;

            return concurrencyStatus;
        }

        private ConcurrencyStatus GetFunctionConcurrencyStatus(string functionId)
        {
            return _concurrencyStatuses.GetOrAdd(functionId, new ConcurrencyStatus());
        }
    }
}
