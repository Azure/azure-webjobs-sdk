// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    internal class DynamicTargetValueProvider : IDynamicTargetValueProvider
    {
        private TimeSpan _cachedInterval = TimeSpan.FromSeconds(30);
        private TimeSpan _expiredSnapshotInterval = TimeSpan.FromSeconds(60);
        private DateTime _lastSnapshotRead = DateTime.MinValue;
        private HostConcurrencySnapshot _lastSnapshot;
        private IConcurrencyStatusRepository _concurrencyStatusRepository;
        private readonly ILogger _logger;
        static SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

        public DynamicTargetValueProvider(IConcurrencyStatusRepository concurrencyStatusRepository, ILoggerFactory loggerFactory)
        {
            _concurrencyStatusRepository = concurrencyStatusRepository;
            _logger = loggerFactory.CreateLogger<DynamicTargetValueProvider>();
        }

        // for tests
        internal DynamicTargetValueProvider(IConcurrencyStatusRepository concurrencyStatusRepository, ILoggerFactory loggerFactory,
            TimeSpan cachedInterval, TimeSpan expiredSnapshotInterval) : this(concurrencyStatusRepository, loggerFactory)
        {
            _cachedInterval = cachedInterval;
            _expiredSnapshotInterval = expiredSnapshotInterval;
        }

        // for tests
        public IConcurrencyStatusRepository ConcurrencyStatusRepository
        {
            set => _concurrencyStatusRepository = value;
        }

        // for tests
        public DateTime LastSnapshotRead
        {
            set => _lastSnapshotRead = value;
        }

        public async Task<int> GetDynamicTargetValueAsync(string functionId)
        {
            DateTime now = DateTime.UtcNow;
            int fallback = -1; // default fallback

            _logger.LogDebug($"Getting dynamic target value for function '{functionId}'");
            await _semaphoreSlim.WaitAsync();
            try
            {
                if (_concurrencyStatusRepository == null)
                {
                    _logger.LogDebug($"Returning fallback. Snapshot repository does not exists.");
                }
                else
                {
                    // Update host snaphsot if cache is expired or snapshot is expired
                    if (now - _lastSnapshotRead > _cachedInterval)
                    {
                        _lastSnapshotRead = now;
                        _lastSnapshot = await _concurrencyStatusRepository.ReadAsync(CancellationToken.None); // TODO: CancellationToken.None
                        _logger.LogDebug($"Snapshot is updated: '{JsonConvert.SerializeObject(_lastSnapshot)}'");
                    }

                    // Return value from 
                    if (_lastSnapshot != null && now - _lastSnapshot.Timestamp < _expiredSnapshotInterval)
                    {
                        if (_lastSnapshot.FunctionSnapshots.TryGetValue(functionId, out FunctionConcurrencySnapshot functionConcurrencySnapshot))
                        {
                            _logger.LogDebug($"Returning '{functionConcurrencySnapshot.Concurrency}' as target value for function '{functionId}'");
                            return functionConcurrencySnapshot.Concurrency;
                        }
                        else
                        {
                            _logger.LogDebug($"Returning fallback. Snapshot for the function '{functionId}' is not found.");
                        }
                    }
                    else
                    {
                        _logger.LogDebug($"Returning fallback. Snapshot for the host is expired or not found.");
                    }
                }
            }
            catch (Exception ex)
            {
                // in case of an exception return fallback
                _logger.LogError($"Returning fallback. An exception was occured during reading dynamic target value for the funciton '{functionId}' due to exception {ex}.");
            }
            finally
            {
                _semaphoreSlim.Release();
            }

            return fallback;
        }
    }
}