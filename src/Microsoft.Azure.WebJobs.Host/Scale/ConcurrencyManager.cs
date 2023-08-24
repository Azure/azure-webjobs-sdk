// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Used to implement collaborative dynamic concurrency management between the host and function triggers.
    /// Function listeners can call <see cref="GetStatus"/> within their listener polling loops to determine the
    /// amount of new work that can be fetched. The manager internally adjusts concurrency based on various
    /// health heuristics.
    /// </summary>
    public class ConcurrencyManager
    {
        internal const int MinConsecutiveIncreaseLimit = 5;
        internal const int MinConsecutiveDecreaseLimit = 3;

        private readonly ILogger _logger;
        private readonly IOptions<ConcurrencyOptions> _options;
        private readonly IConcurrencyThrottleManager _concurrencyThrottleManager;
        private readonly bool _enabled;

        private ConcurrencyThrottleAggregateStatus _throttleStatus;

#nullable disable
        // for mock testing only
        internal ConcurrencyManager()
        {
        }
#nullable enable

        public ConcurrencyManager(IOptions<ConcurrencyOptions> options, ILoggerFactory loggerFactory, IConcurrencyThrottleManager concurrencyThrottleManager)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _concurrencyThrottleManager = concurrencyThrottleManager ?? throw new ArgumentNullException(nameof(concurrencyThrottleManager));

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }
            _logger = loggerFactory.CreateLogger(LogCategories.Concurrency);
            _enabled = _options.Value.DynamicConcurrencyEnabled;

            EffectiveCoresCount = Utility.GetEffectiveCoresCount();

            _throttleStatus = new ConcurrencyThrottleAggregateStatus();
        }

        /// <summary>
        /// Gets a value indicating whether dynamic concurrency is enabled.
        /// </summary>
        public bool Enabled => _enabled;

        /// <summary>
        /// Gets the current throttle status.
        /// </summary>
        internal virtual ConcurrencyThrottleAggregateStatus ThrottleStatus => _throttleStatus;

        internal int EffectiveCoresCount { get; set; }

        internal ConcurrentDictionary<string, ConcurrencyStatus> ConcurrencyStatuses = new ConcurrentDictionary<string, ConcurrencyStatus>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the concurrency status for the specified function. 
        /// </summary>
        /// <param name="functionId">This should be the full ID, as returned by <see cref="FunctionDescriptor.ID"/>.</param>
        /// <returns>The updated concurrency status.</returns>
        /// <remarks>
        /// This method shouldn't be called concurrently for the same function ID. For trigger bindings using a shared listener pattern,
        /// the ID passed to this function should be the shared ID, and that ID should also be annotated on the
        /// <see cref="Microsoft.Azure.WebJobs.Host.Triggers.ITriggerBinding"/> implementation using <see cref="SharedListenerAttribute"/>.
        /// </remarks>
        public ConcurrencyStatus GetStatus(string functionId)
        {
            if (string.IsNullOrEmpty(functionId))
            {
                throw new ArgumentNullException(nameof(functionId));
            }

            // because this method won't be called concurrently for the same function ID, we can make
            // updates to the function specific status below without locking.
            var functionConcurrencyStatus = GetFunctionConcurrencyStatus(functionId);

            if (!functionConcurrencyStatus.CanAdjustConcurrency())
            {
                // if we've made an adjustment recently for this function, just return the
                // current status
                return functionConcurrencyStatus;
            }

            // determine whether any throttles are currently enabled
            _throttleStatus = _concurrencyThrottleManager.GetStatus();
            if (_throttleStatus.State == ThrottleState.Unknown)
            {
                // if we're un an unknown state, we'll make no moves
                // however, we will continue to take work at the current concurrency level
                return functionConcurrencyStatus;
            }

            if (_throttleStatus.State == ThrottleState.Disabled)
            {
                if (CanIncreaseConcurrency(functionConcurrencyStatus))
                {
                    _logger.FunctionConcurrencyIncrease(functionId);
                    functionConcurrencyStatus.IncreaseConcurrency();
                }
            }
            else if (CanDecreaseConcurrency(functionConcurrencyStatus))
            {
                string enabledThrottles = _throttleStatus.EnabledThrottles != null ? string.Join(",", _throttleStatus.EnabledThrottles) : string.Empty;
                _logger.FunctionConcurrencyDecrease(functionId, enabledThrottles);

                functionConcurrencyStatus.DecreaseConcurrency();
            }

            functionConcurrencyStatus.LogUpdates(_logger);

            return functionConcurrencyStatus;
        }

        internal virtual HostConcurrencySnapshot GetSnapshot()
        {
            var functionSnapshots = ConcurrencyStatuses.Values.ToDictionary(p => p.FunctionId, q => new FunctionConcurrencySnapshot { Concurrency = q.CurrentConcurrency }, StringComparer.OrdinalIgnoreCase);
            var hostSnapshot = new HostConcurrencySnapshot
            {
                Timestamp = DateTime.UtcNow,
                NumberOfCores = EffectiveCoresCount,
                FunctionSnapshots = functionSnapshots
            };
            return hostSnapshot;
        }

        internal virtual void ApplySnapshot(HostConcurrencySnapshot hostSnapshot)
        {
            if (hostSnapshot == null)
            {
                throw new ArgumentNullException(nameof(hostSnapshot));
            }

            if (hostSnapshot.FunctionSnapshots != null)
            {
                foreach (var functionSnapshot in hostSnapshot.FunctionSnapshots)
                {
                    int concurrency = GetCoreAdjustedConcurrency(functionSnapshot.Value.Concurrency, hostSnapshot.NumberOfCores, EffectiveCoresCount);
                    if (concurrency > _options.Value.MaximumFunctionConcurrency)
                    {
                        // don't apply the snapshot if its concurrency value is greater than the
                        // maximum configured level
                        continue;
                    }

                    functionSnapshot.Value.Concurrency = concurrency;

                    // Since we may be initializing for functions that haven't run yet, if the snapshot contains
                    // stale functions, they'll be added. When we write snapshots, we prune stale entries though.
                    var concurrencyStatus = GetFunctionConcurrencyStatus(functionSnapshot.Key);
                    _logger.LogInformation($"Applying status snapshot for function {functionSnapshot.Key} (Concurrency: {functionSnapshot.Value.Concurrency})");
                    concurrencyStatus.ApplySnapshot(functionSnapshot.Value);
                }
            }
        }

        internal static int GetCoreAdjustedConcurrency(int concurrency, int otherCores, int cores)
        {
            if (cores != otherCores)
            {
                // To allow for variance across machines, we compute the effective concurrency
                // based on number of cores. When running in an App Service plan, all instances will have
                // the same VM specs. When running in the Consumption plan, VMs may differ. In the latter case,
                // if the snapshot was taken on a VM with a different core count than ours, the adjusted
                // concurency we compute may not be optimal, but it's a starting point that we'll dynamically
                // adjust from as needed.
                float concurrencyPerCore = (float)concurrency / otherCores;
                int adjustedConcurrency = (int)(cores * concurrencyPerCore);

                return Math.Max(1, adjustedConcurrency);
            }

            return concurrency;
        }

        private bool CanIncreaseConcurrency(ConcurrencyStatus concurrencyStatus)
        {
            // we're in a throttle disabled state
            if (_throttleStatus?.ConsecutiveCount < MinConsecutiveIncreaseLimit)
            {
                // only increase if we've been healthy for a while
                return false;
            }

            return concurrencyStatus.CanIncreaseConcurrency(_options.Value.MaximumFunctionConcurrency);
        }

        private bool CanDecreaseConcurrency(ConcurrencyStatus concurrencyStatus)
        {
            // we're in a throttle enabled state
            if (_throttleStatus?.ConsecutiveCount < MinConsecutiveDecreaseLimit)
            {
                // only decrease if we've been unhealthy for a while
                return false;
            }

            return concurrencyStatus.CanDecreaseConcurrency();
        }

        internal void FunctionStarted(string functionId)
        {
            if (string.IsNullOrEmpty(functionId))
            {
                throw new ArgumentNullException(nameof(functionId));
            }
            
            // Here we TryGet rather than GetOrAdd so that we're only performing
            // this overhead for functions that are actually using dynamic concurrency.
            // For DC enabled functions, we won't see an invocation until after the first
            // call to GetStatus, which will create the ConcurrencyStatus.
            if (ConcurrencyStatuses.TryGetValue(functionId, out ConcurrencyStatus concurrencyStatus))
            {
                concurrencyStatus.FunctionStarted();
            }
        }

        internal void FunctionCompleted(string functionId, TimeSpan latency)
        {
            if (string.IsNullOrEmpty(functionId))
            {
                throw new ArgumentNullException(nameof(functionId));
            }

            // Here we TryGet rather than GetOrAdd so that we're only performing
            // this overhead for functions that are actually using dynamic concurrency.
            // For DC enabled functions, we won't see an invocation until after the first
            // call to GetStatus, which will create the ConcurrencyStatus.
            if (ConcurrencyStatuses.TryGetValue(functionId, out ConcurrencyStatus concurrencyStatus))
            {
                concurrencyStatus.FunctionCompleted(latency);
            }
        }

        private ConcurrencyStatus GetFunctionConcurrencyStatus(string functionId)
        {
            return ConcurrencyStatuses.GetOrAdd(functionId, new ConcurrencyStatus(functionId, this));
        }
    }
}
