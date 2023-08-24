// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Service responsible for startup time application of Dynamic Concurrency snapshots,
    /// as well as periodic background persistence of status snapshots.
    /// </summary>
    internal class ConcurrencyManagerService : IHostedService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IOptions<ConcurrencyOptions> _options;
        private readonly IConcurrencyStatusRepository _statusRepository;
        private readonly System.Timers.Timer _statusPersistenceTimer;
        private readonly ConcurrencyManager _concurrencyManager;
        private readonly IFunctionIndexProvider _functionIndexProvider;
        private readonly IPrimaryHostStateProvider _primaryHostStateProvider;

        private HostConcurrencySnapshot? _lastSnapshot;
        private bool _disposed;

        public ConcurrencyManagerService(IOptions<ConcurrencyOptions> options, ILoggerFactory loggerFactory, ConcurrencyManager concurrencyManager, IConcurrencyStatusRepository statusRepository, IFunctionIndexProvider functionIndexProvider, IPrimaryHostStateProvider primaryHostStateProvider)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _concurrencyManager = concurrencyManager ?? throw new ArgumentNullException(nameof(concurrencyManager));
            _statusRepository = statusRepository ?? throw new ArgumentNullException(nameof(statusRepository));
            _functionIndexProvider = functionIndexProvider ?? throw new ArgumentNullException(nameof(functionIndexProvider));
            _primaryHostStateProvider = primaryHostStateProvider ?? throw new ArgumentNullException(nameof(primaryHostStateProvider));

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }
            _logger = loggerFactory.CreateLogger(LogCategories.Concurrency);

            _statusPersistenceTimer = new System.Timers.Timer
            {
                AutoReset = false,
                Interval = 10000
            };
            _statusPersistenceTimer.Elapsed += OnPersistenceTimer;
        }

        internal System.Timers.Timer StatusPersistenceTimer => _statusPersistenceTimer;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_options.Value.DynamicConcurrencyEnabled && _options.Value.SnapshotPersistenceEnabled)
            {
                await ApplySnapshotAsync();

                _statusPersistenceTimer.Start();
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _statusPersistenceTimer?.Stop();
            return Task.CompletedTask;
        }

        private async Task ApplySnapshotAsync()
        {
            try
            {
                // one time application of status snapshot on startup
                var snapshot = await _statusRepository.ReadAsync(CancellationToken.None);
                if (snapshot != null)
                {
                    _lastSnapshot = snapshot;
                    _concurrencyManager.ApplySnapshot(snapshot);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying concurrency snapshot.");
            }
        }

        internal async void OnPersistenceTimer(object sender, ElapsedEventArgs e)
        {
            await OnPersistenceTimer();
        }

        internal async Task OnPersistenceTimer()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (_primaryHostStateProvider.IsPrimary)
                {
                    await WriteSnapshotAsync();
                }
            }
            catch (Exception ex)
            {
                // Don't allow background exceptions to escape
                _logger.LogError(ex, "Error persisting concurrency snapshot.");
            }

            if (!_disposed)
            {
                _statusPersistenceTimer.Start();
            }
        }

        internal async Task WriteSnapshotAsync()
        {
            var snapshot = _concurrencyManager.GetSnapshot();

            // Only persist snapshots for functions that are in our current index. This ensures
            // we prune any stale entries from a previously applied snapshot.
            // Note that functions using a shared listener are treated specially here.
            var functionIndex = await _functionIndexProvider.GetAsync(CancellationToken.None);
            var functions = functionIndex.ReadAll().ToLookup(p => p.Descriptor.SharedListenerId ?? p.Descriptor.Id, StringComparer.OrdinalIgnoreCase);
            snapshot.FunctionSnapshots = snapshot.FunctionSnapshots.Where(p => functions.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value);

            if (!snapshot.Equals(_lastSnapshot))
            {
                await _statusRepository.WriteAsync(snapshot, CancellationToken.None);

                _lastSnapshot = snapshot;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _statusPersistenceTimer.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
