// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    internal class DynamicTargetValueProvider : IDynamicTargetValueProvider
    {
        private const int DynamicConcurrencyStabilizationTimeInSeconds = 60;  //default value in the scale controller
        private const int CacheExpirationTimeDynamicConcurrencyDesiredMetricValueInSeconds = 60; //default value in the scale controller
        private const int CacheExpirationGracePeriodInSeconds = 30; //default value in the scale controller
        private const int DynamicConcurrencyStabilizationRange = 1; //default value in the scale controller

        private IConcurrencyStatusRepository _concurrencyStatusRepository;
        private DateTime lastFunctionSnapshotCacheUpdateTime;
        private ConcurrentDictionary<string, FunctionSnapshotCacheEntry> dynamicConcurrencyFunctionCache;
        private SemaphoreSlim dynamicConcurrencyCacheUpdateLock;

        public DynamicTargetValueProvider(IConcurrencyStatusRepository concurrencyStatusRepository)
        {
            _concurrencyStatusRepository = concurrencyStatusRepository;
            lastFunctionSnapshotCacheUpdateTime = DateTime.MinValue;
            dynamicConcurrencyFunctionCache = new ConcurrentDictionary<string, FunctionSnapshotCacheEntry>();
            this.dynamicConcurrencyCacheUpdateLock = new SemaphoreSlim(1);
        }

        public async Task<int> GetDynamicTargetValue(string functionId, bool isDynamicConcurrencyEnabled)
        {
            int fallbackValue = -1;
            if (isDynamicConcurrencyEnabled)
            {
                DateTime currentTime = DateTime.UtcNow;
                FunctionSnapshotCacheEntry functionSnapshotCacheEntry;
                if (IsCachedMetricValueValid(functionId, currentTime, out functionSnapshotCacheEntry))
                {
                    //ExtensionLogEventSource.Log.SiteInformation(site.Name, $"Using the cached desiredMetricValue of {functionSnapshotCacheEntry.MetricValue} for function {functionName}.");
                    return functionSnapshotCacheEntry.MetricValue;
                }
                
                if (IsCachedMetricValidFallback(functionSnapshotCacheEntry, currentTime))
                {
                    //ExtensionLogEventSource.Log.SiteInformation(site.Name, $"Cached desiredMetricValue of {functionSnapshotCacheEntry.MetricValue} is valid as a fallback for function {functionName}.");
                    fallbackValue = functionSnapshotCacheEntry.MetricValue;
                }

                if (_concurrencyStatusRepository == null)
                {
                    if (dynamicConcurrencyCacheUpdateLock.Wait(0))
                    {
                        try
                        {
                            if ((currentTime - lastFunctionSnapshotCacheUpdateTime).TotalSeconds < DynamicConcurrencyStabilizationTimeInSeconds)
                            {
                                //Only want each site to read from blob storage once every DynamicConcurrencyStabilizationTimeInSeconds
                                //ExtensionLogEventSource.Log.SiteWarning(site.Name, $"Site {site.Name} has already read from blob storage for dynamic concurrency value within the past {config.DynamicConcurrencyStabilizationTimeInSeconds} seconds. Last access to blob storage was at {lastFunctionSnapshotCacheUpdateTime}.");
                                return fallbackValue;
                            }

                            //DynamicConcurrencyStatusBlob dynamicConcurrencyDataJson = await ParseJSONFromDynamicConcurrencyBlobAsync();
                            HostConcurrencySnapshot hostConcurrencySnapshot = await _concurrencyStatusRepository.ReadAsync(CancellationToken.None);

                            if (hostConcurrencySnapshot == null)
                            {
                                //ExtensionLogEventSource.Log.SiteWarning(site.Name, $"Unable to deserialize dynamic concurrency status blob.");
                                return fallbackValue;
                            }

                            UpdateCacheForAllFunctions(hostConcurrencySnapshot, currentTime);
                            lastFunctionSnapshotCacheUpdateTime = currentTime;

                            FunctionSnapshotCacheEntry newCacheEntry;
                            if (GetCachedFunctionSnapshotForFunction(functionId, out newCacheEntry) && newCacheEntry.MetricIsStable)
                            {
                                //ExtensionLogEventSource.Log.SiteInformation(context.AppName, $"Using stabilized value of {newCacheEntry.MetricValue} for function {functionName}");
                                return newCacheEntry.MetricValue;
                            }
                            else
                            {
                                return fallbackValue;
                            }
                        }
                        catch // (Exception e)
                        {
                            //ExtensionLogEventSource.Log.SiteWarning(site.Name, $"Unable to read from the blob where dynamic concurrency data is stored for site {site.Name} due to exception {e}.");
                        }
                        finally
                        {
                            dynamicConcurrencyCacheUpdateLock.Release();
                        }
                    }
                }
                else
                {
                    //ExtensionLogEventSource.Log.SiteInformation(site.Name, $"Target Based Scaling Algorithm was unable to connect to the blob where dynamic concurrency data is stored for site {site.Name} upon instantiation.");
                }
            }
            return fallbackValue;


            // Returning fallback value since Dynamic Concurrency is not enabled
            // fallbackValue can either be user-defined, or the default value for service bus and eventhub
            //bool isDefault = fallbackValue == DefaultTargetEventHubMetric || fallbackValue == DefaultTargetServiceBusMetric;
            //ExtensionLogEventSource.Log.SiteInformation(site.Name, $"Using {(isDefault ? "default" : "user-defined")} desiredMetricValue of {fallbackValue} for {triggerName} trigger");
            //return context.StaticTargetValue;

            //int desiredWorkerCount = (int)Math.Ceiling((decimal)targetContext.Metrics.Last().MessageCount / desiredMetricValue);

            //ExtensionLogEventSource.Log.SiteScaleVote(site.Name, queueTriggerId, scaleResult.ToString(), scaleReason);
            //return desiredWorkerCount - context.WorkerCount;
        }

        internal bool IsCachedMetricValidFallback(FunctionSnapshotCacheEntry cacheEntry, DateTime currentTime)
        {
            if (cacheEntry == null)
            {
                return false;
            }

            if (!cacheEntry.MetricIsStable)
            {
                return false;
            }

            double secondsExpired = (currentTime - cacheEntry.MetricCachedAt).TotalSeconds - CacheExpirationTimeDynamicConcurrencyDesiredMetricValueInSeconds;

            if (secondsExpired > CacheExpirationGracePeriodInSeconds)
            {
                return false;
            }

            return true;
        }

        internal bool IsCachedMetricValueValid(string functionName, DateTime currentTime, out FunctionSnapshotCacheEntry functionSnapshotCacheEntry)
        {
            if (dynamicConcurrencyFunctionCache.TryGetValue(functionName, out functionSnapshotCacheEntry))
            {
                if (!functionSnapshotCacheEntry.MetricIsStable)
                {
                    //ExtensionLogEventSource.Log.SiteInformation(site.Name, $"Cached desiredMetricValue of {functionSnapshotCacheEntry.MetricValue} is not stable for function {functionName}. " +
                    //$"Attempting to read a new dynamic concurrency desiredMetricValue from blob storage.");
                    return false;
                }

                if ((currentTime - functionSnapshotCacheEntry.MetricCachedAt).TotalSeconds > CacheExpirationTimeDynamicConcurrencyDesiredMetricValueInSeconds)
                {
                    //ExtensionLogEventSource.Log.SiteInformation(site.Name, $"Cached desiredMetricValue {functionSnapshotCacheEntry.MetricValue} has expired for function {functionName}. " +
                    //$"Attempting to read a new dynamic concurrency desiredMetricValue from blob storage.");
                    return false;
                }
                return true;
            }
            else
            {
                //ExtensionLogEventSource.Log.SiteInformation(site.Name, $"No cached desiredMetricValue exists for function {functionName}. " +
                //    $"Attempting to read a new dynamic concurrency desiredMetricValue from blob storage.");
                return false;
            }
        }

        internal void UpdateSnapshotCacheForFunction(string functionName, int desiredMetricValue, DateTime currentTime, bool isStable)
        {
            FunctionSnapshotCacheEntry newFunctionSnapshot = new FunctionSnapshotCacheEntry(desiredMetricValue, currentTime, isStable);

            // In the time that this thread was running, another thread could have updated the dictionary with a more recently pulled desiredMetricValue. In that case, we don't want to 
            // populate the dictionary with our value, and in either case we just return whatever value is currently in the direction (post-population or not)
            dynamicConcurrencyFunctionCache.AddOrUpdate(functionName, newFunctionSnapshot,
                (key, currentFunctionSnapshot) =>
                    currentFunctionSnapshot.MetricCachedAt < currentTime ? newFunctionSnapshot : currentFunctionSnapshot);
        }

        internal void UpdateCacheForAllFunctions(HostConcurrencySnapshot dcBlob, DateTime currentTime)
        {
            if (dcBlob.FunctionSnapshots != null)
            {
                foreach (var fullFunctionName in dcBlob.FunctionSnapshots.Keys)
                {
                    if (dcBlob.FunctionSnapshots.TryGetValue(fullFunctionName, out FunctionConcurrencySnapshot functionSnapshot))
                    {
                        int newDesiredMetricValue = functionSnapshot.Concurrency;
                        string functionName = GetFunctionName(fullFunctionName);
                        bool isStable = IsNewMetricValueStableForFunction(functionName, newDesiredMetricValue);
                        UpdateSnapshotCacheForFunction(functionName, newDesiredMetricValue, currentTime, isStable);
                    }
                }
            }
        }

        internal bool GetCachedFunctionSnapshotForFunction(string functionName, out FunctionSnapshotCacheEntry functionSnapshotCacheEntry)
        {
            return dynamicConcurrencyFunctionCache.TryGetValue(functionName, out functionSnapshotCacheEntry);
        }

        internal bool IsNewMetricValueStableForFunction(string functionName, int newMetricValue)
        {
            if (newMetricValue < 0)
            {
                return false;
            }

            if (!GetCachedFunctionSnapshotForFunction(functionName, out FunctionSnapshotCacheEntry oldEntry))
            {
                return false;
            }

            int oldMetricValue = oldEntry.MetricValue;
            if (newMetricValue <= oldMetricValue + DynamicConcurrencyStabilizationRange &&
                newMetricValue >= oldMetricValue - DynamicConcurrencyStabilizationRange)
            {
                return true;
            }
            return false;
        }

        internal static string GetFunctionName(string fullFunctionName)
        {
            if (string.IsNullOrEmpty(fullFunctionName))
            {
                return "";
            }
            return fullFunctionName.Split('.').Last();
        }
    }
}

internal class FunctionSnapshotCacheEntry
{
    internal int MetricValue { get; set; }

    internal DateTime MetricCachedAt { get; set; }

    internal bool MetricIsStable { get; set; }


    public FunctionSnapshotCacheEntry(int metricValue, DateTime metricCachedAt, bool metricIsStable)
    {
        MetricValue = metricValue;
        MetricCachedAt = metricCachedAt;
        MetricIsStable = metricIsStable;
    }
}