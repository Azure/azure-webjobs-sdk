// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Class to get scaling votes for all the triggers or for an individual trigger.
    /// </summary>
    internal class ScaleManager : IScaleManager
    {
        private readonly IScaleMonitorManager _monitorManager;
        private readonly ITargetScalerManager _targetScalerManager;
        private readonly IScaleMetricsRepository _metricsRepository;
        private readonly IConcurrencyStatusRepository _concurrencyStatusRepository;
        private readonly ILogger _logger;
        private readonly HashSet<string> _targetScalersInError;
        private readonly IConfiguration _configuration;
        private IOptions<ScaleOptions> _scaleOptions;

        public ScaleManager(
            IScaleMonitorManager monitorManager,
            ITargetScalerManager targetScalerManager,
            IScaleMetricsRepository metricsRepository,
            IConcurrencyStatusRepository concurrencyStatusRepository,
            IOptions<ScaleOptions> scaleConfiguration,
            ILoggerFactory loggerFactory,
            IConfiguration configuration)
        {
            _monitorManager = monitorManager;
            _targetScalerManager = targetScalerManager;
            _metricsRepository = metricsRepository;
            _concurrencyStatusRepository = concurrencyStatusRepository;
            _logger = loggerFactory?.CreateLogger<ScaleManager>();
            _targetScalersInError = new HashSet<string>();
            _scaleOptions = scaleConfiguration;
            _configuration = configuration;
        }

        // for mock testing only
        internal ScaleManager()
        {
        }


        /// <summary>
        /// Gets overall scale status for all the triggers.
        /// </summary>
        /// <param name="context">The scale status context</param>
        /// <returns>A task that returns <see cref="ScaleStatus"/> for all ther triggers.</returns>
        public async Task<ScaleStatus> GetScaleStatusAsync(ScaleStatusContext context)
        {
            var (scaleMonitorsToProcess, targetScalersToProcess) = GetScalersToSample();

            var scaleMonitorStatuses = await GetScaleMonitorsResultAsync(context, scaleMonitorsToProcess);
            var targetScalerStatuses = await GetTargetScalersResultAsync(context, targetScalersToProcess);

            return new ScaleStatus
            {
                Vote = GetAggregateScaleVote(scaleMonitorStatuses.Values.Select(x => x.Vote).Union(targetScalerStatuses.Select(x => x.Value.Vote)), context, _logger),
                TargetWorkerCount = targetScalerStatuses.Any() ? targetScalerStatuses.Max(x => x.Value.TargetWorkerCount) : null,
                FunctionScaleStatuses = scaleMonitorStatuses.Concat(targetScalerStatuses).ToDictionary(s => s.Key, s => s.Value)
            };
        }

        private async Task<IDictionary<string, ScaleStatus>> GetScaleMonitorsResultAsync(ScaleStatusContext context, IEnumerable<IScaleMonitor> scaleMonitorsToProcess)
        {
            Dictionary<string, ScaleStatus> votes = new Dictionary<string, ScaleStatus>();
            if (scaleMonitorsToProcess != null && scaleMonitorsToProcess.Any())
            {
                // get the collection of current metrics for each monitor
                var monitorMetrics = await _metricsRepository.ReadMetricsAsync(scaleMonitorsToProcess);

                _logger.LogDebug($"Computing scale status (WorkerCount={context.WorkerCount})");
                _logger.LogDebug($"{monitorMetrics.Count} scale monitors to sample");

                // for each monitor, ask it to return its scale status (vote) based on
                // the metrics and context info (e.g. worker count)
                foreach (var pair in monitorMetrics)
                {
                    var monitor = pair.Key;
                    var metrics = pair.Value;

                    try
                    {
                        // create a new context instance to avoid modifying the
                        // incoming context
                        var scaleStatusContext = new ScaleStatusContext
                        {
                            WorkerCount = context.WorkerCount,
                            Metrics = metrics
                        };
                        var result = monitor.GetScaleStatus(scaleStatusContext);

                        _logger.LogDebug($"Monitor '{monitor.Descriptor.Id}' voted '{result.Vote.ToString()}'");
                        string key = monitor.Descriptor.FunctionId ?? monitor.Descriptor.Id;
                        votes.Add(key, new ScaleStatus()
                        {
                            Vote = result.Vote
                        });
                    }
                    catch (Exception exc) when (!exc.IsFatal())
                    {
                        // if a particular monitor fails, log and continue
                        _logger.LogError(exc, $"Failed to query scale status for monitor '{monitor.Descriptor.Id}'.");
                    }
                }
            }
            else
            {
                // no monitors registered
                // this can happen if the host is offline
            }

            return votes;
        }

        private async Task<IDictionary<string, ScaleStatus>> GetTargetScalersResultAsync(ScaleStatusContext context, IEnumerable<ITargetScaler> targetScalersToProcess)
        {
            Dictionary<string, ScaleStatus> targetScaleVotes = new Dictionary<string, ScaleStatus>();

            if (targetScalersToProcess != null && targetScalersToProcess.Any())
            {
                _logger.LogDebug($"{targetScalersToProcess.Count()} target scalers to sample");
                HostConcurrencySnapshot snapshot = null;
                try
                {
                    snapshot = await _concurrencyStatusRepository.ReadAsync(CancellationToken.None);
                }
                catch (Exception exc) when (!exc.IsFatal())
                {
                    _logger.LogError(exc, $"Failed to read concurrency status repository");
                }

                foreach (var targetScaler in targetScalersToProcess)
                {
                    try
                    {
                        TargetScalerContext targetScaleStatusContext = new TargetScalerContext();
                        if (snapshot != null)
                        {
                            if (snapshot.FunctionSnapshots.TryGetValue(targetScaler.TargetScalerDescriptor.FunctionId, out var functionSnapshot))
                            {
                                targetScaleStatusContext.InstanceConcurrency = functionSnapshot.Concurrency;
                                _logger.LogDebug($"Snapshot dynamic concurrency for target scaler '{targetScaler.TargetScalerDescriptor.FunctionId}' is '{functionSnapshot.Concurrency}'");
                            }
                        }
                        TargetScalerResult result = null;
                        try
                        {
                            result = await targetScaler.GetScaleResultAsync(targetScaleStatusContext);
                        }
                        catch (NotSupportedException ex)
                        {
                            string targetScalerUniqueId = GetTargetScalerFunctionUniqueId(targetScaler);
                            _logger.LogWarning($"Unable to use target based scaling for Function '{targetScaler.TargetScalerDescriptor.FunctionId}'. Metrics monitoring will be used.", ex);
                            _targetScalersInError.Add(targetScalerUniqueId);

                            // Adding ScaleVote.None vote
                            result = new TargetScalerResult
                            {
                                TargetWorkerCount = context.WorkerCount
                            };
                        }
                        _logger.LogDebug($"Target worker count for '{targetScaler.TargetScalerDescriptor.FunctionId}' is '{result.TargetWorkerCount}'");

                        ScaleVote vote = ScaleVote.None;
                        if (context.WorkerCount > result.TargetWorkerCount)
                        {
                            vote = ScaleVote.ScaleIn;
                        }
                        else if (context.WorkerCount < result.TargetWorkerCount)
                        {
                            vote = ScaleVote.ScaleOut;
                        }

                        targetScaleVotes.Add(targetScaler.TargetScalerDescriptor.FunctionId, new ScaleStatus
                        {
                            TargetWorkerCount = result.TargetWorkerCount,
                            Vote = vote
                        });
                    }
                    catch (Exception exc) when (!exc.IsFatal())
                    {
                        // if a particular target scaler fails, log and continue
                        _logger.LogError(exc, $"Failed to query scale result for target scaler '{targetScaler.TargetScalerDescriptor.FunctionId}'.");
                    }
                }
            }
            return targetScaleVotes;
        }

        /// <summary>
        /// Returns scale monitors and target scalers we want to use based on the configuration.
        /// Scaler monitor will be ignored if a target scaler is defined in the same extensions assembly and TBS is enabled.
        /// </summary>
        /// <param name="scaleMonitorsToSample">Scale monitor to process.</param>
        /// <param name="targetScalersToSample">Target scaler to process.</param>
        internal virtual (List<IScaleMonitor>, List<ITargetScaler>) GetScalersToSample()
        {
            var scaleMonitors = _monitorManager.GetMonitors();
            var targetScalers = _targetScalerManager.GetTargetScalers();

            var scaleMonitorsToSample = new List<IScaleMonitor>();
            var targetScalersToSample = new List<ITargetScaler>();

            // Check if TBS enabled on app level
            if (_scaleOptions.Value.IsTargetScalingEnabled)
            {
                HashSet<string> targetScalerFunctions = new HashSet<string>();
                foreach (var scaler in targetScalers)
                {
                    string scalerUniqueId = GetTargetScalerFunctionUniqueId(scaler);
                    if (!_targetScalersInError.Contains(scalerUniqueId))
                    {
                        string assemblyName = GetAssemblyName(scaler.GetType());
                        bool featureEnabled = _configuration.GetSection(Constants.FunctionsHostingConfigSectionName).GetValue<string>(assemblyName) == "1";
                        if (featureEnabled)
                        {
                            targetScalersToSample.Add(scaler);
                            targetScalerFunctions.Add(scalerUniqueId);
                        }
                    }
                }

                foreach (var monitor in scaleMonitors)
                {
                    string monitorUniqueId = GetScaleMonitorFunctionUniqueId(monitor);
                    // Check if target based scaler exists for the function
                    if (!targetScalerFunctions.Contains(monitorUniqueId))
                    {
                        scaleMonitorsToSample.Add(monitor);
                    }
                }
            }
            else
            {
                scaleMonitorsToSample.AddRange(scaleMonitors);
            }

            return (scaleMonitorsToSample, targetScalersToSample);
        }

        internal static ScaleVote GetAggregateScaleVote(IEnumerable<ScaleVote> votes, ScaleStatusContext context, ILogger logger)
        {
            ScaleVote vote = ScaleVote.None;
            if (votes.Any())
            {
                // aggregate all the votes into a single vote
                if (votes.Any(p => p == ScaleVote.ScaleOut))
                {
                    // scale out if at least 1 monitor requires it
                    logger?.LogDebug("Scaling out based on votes");
                    vote = ScaleVote.ScaleOut;
                }
                else if (context.WorkerCount > 0 && votes.All(p => p == ScaleVote.ScaleIn))
                {
                    // scale in only if all monitors vote scale in
                    logger?.LogDebug("Scaling in based on votes");
                    vote = ScaleVote.ScaleIn;
                }
            }
            else if (context.WorkerCount > 0)
            {
                // if no functions exist or are enabled we'll scale in
                logger?.LogDebug("No enabled functions or scale votes so scaling in");
                vote = ScaleVote.ScaleIn;
            }

            return vote;
        }

        private string GetTargetScalerFunctionUniqueId(ITargetScaler scaler)
        {
            return $"{GetAssemblyName(scaler.GetType())}-{scaler.TargetScalerDescriptor.FunctionId}";
        }


        private string GetScaleMonitorFunctionUniqueId(IScaleMonitor monitor)
        {
            return $"{GetAssemblyName(monitor.GetType())}-{monitor.Descriptor.FunctionId}";
        }


        protected string GetAssemblyName(Type type)
        {
            return type.Assembly.GetName().Name;
        }
    }
}
