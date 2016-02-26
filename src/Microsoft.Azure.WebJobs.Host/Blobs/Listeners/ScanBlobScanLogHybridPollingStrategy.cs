using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Listeners
{
    internal sealed class ScanBlobScanLogHybridPollingStrategy : IBlobListenerStrategy
    {
        private static readonly TimeSpan TwoSeconds = TimeSpan.FromSeconds(2);
        private enum PollStrategy { ScanBlobs, ScanLogs, NotDetermined };
        private int _scanBlobThreshold = 5000;
        private readonly IDictionary<IStorageBlobContainer, PollStrategy> _pollStrategies;
        private readonly IDictionary<IStorageBlobContainer, ICollection<ITriggerExecutor<IStorageBlob>>> _registrations;
        private ScanContainersStrategy _scanContainerStrategy;
        private PollLogsStrategy _pollLogStrategy;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _disposed;

        public ScanBlobScanLogHybridPollingStrategy() : base()
        {
            _registrations = new Dictionary<IStorageBlobContainer, ICollection<ITriggerExecutor<IStorageBlob>>>(
                new StorageBlobContainerComparer());
            _pollStrategies = new Dictionary<IStorageBlobContainer, PollStrategy>(
                new StorageBlobContainerComparer());
            _scanContainerStrategy = new ScanContainersStrategy();
            _pollLogStrategy = new PollLogsStrategy();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void Start()
        {
            ThrowIfDisposed();
            // can not start the 2 strategies till all regiesterations are done
            ScnaContainersAndRegisterWithStrategy(_cancellationTokenSource.Token);
            _scanContainerStrategy.Start();
            _pollLogStrategy.Start();
        }

        public void Cancel()
        {
            ThrowIfDisposed();
            _scanContainerStrategy.Cancel();
            _pollLogStrategy.Cancel();
            _cancellationTokenSource.Cancel();
        }

        public Task RegisterAsync(IStorageBlobContainer container, ITriggerExecutor<IStorageBlob> triggerExecutor, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            // Register and Execute are not concurrency-safe.
            // Avoiding calling Register while Execute is running is the caller's responsibility.

            ICollection<ITriggerExecutor<IStorageBlob>> containerRegistrations;
            if (_registrations.ContainsKey(container))
            {
                containerRegistrations = _registrations[container];
            }
            else
            {
                containerRegistrations = new List<ITriggerExecutor<IStorageBlob>>();
                _registrations.Add(container, containerRegistrations);
            }

            containerRegistrations.Add(triggerExecutor);
            return Task.FromResult(0);
        }

        public async Task<TaskSeriesCommandResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            await Task.WhenAll(_scanContainerStrategy.ExecuteAsync(cancellationToken),
                                _pollLogStrategy.ExecuteAsync(cancellationToken));

            // Run subsequent iterations at 2 second intervals.
            return new TaskSeriesCommandResult(wait: Task.Delay(TwoSeconds));
        }

        public void Notify(IStorageBlob blobWritten)
        {
            ThrowIfDisposed();
            _scanContainerStrategy.Notify(blobWritten);
            _pollLogStrategy.Notify(blobWritten);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _pollLogStrategy.Dispose();
                _scanContainerStrategy.Dispose();
                _cancellationTokenSource.Dispose();
                _disposed = true;
            }
        }

        private void ScnaContainersAndRegisterWithStrategy(CancellationToken cancellationToken)
        {
            foreach (IStorageBlobContainer container in _registrations.Keys)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                IStorageBlobResultSegment results;

                try
                {
                    // Non-async is correct here. ScanContainers occurs on a background thread. Unless it blocks, no one
                    // else is around to observe the results.
                    results = container.ListBlobsSegmentedAsync(prefix: null, useFlatBlobListing: true, blobListingDetails: WindowsAzure.Storage.Blob.BlobListingDetails.None, maxResults: _scanBlobThreshold, currentToken: null, options: null, operationContext: null,
                        cancellationToken: CancellationToken.None).GetAwaiter().GetResult();
                }
                catch (StorageException exception)
                {
                    if (exception.IsNotFound())
                    {
                        return;
                    }
                    else
                    {
                        throw;
                    }
                }

                // decide which strategy to use based on the existance of continuation token
                // if one exists it means there are more use PollLogStrategy otherwise use 
                // ScanBlobStrategy

                if (results.ContinuationToken != null)
                {
                    _pollStrategies.Add(container,PollStrategy.ScanLogs);
                    foreach (var triggerExecuter in _registrations[container])
                    {
                        _pollLogStrategy.RegisterAsync(container, triggerExecuter, _cancellationTokenSource.Token).GetAwaiter().GetResult();
                    }
                }
                else
                {
                    _pollStrategies.Add(container, PollStrategy.ScanBlobs);
                    foreach (var triggerExecuter in _registrations[container])
                    {
                        _scanContainerStrategy.RegisterAsync(container, triggerExecuter, _cancellationTokenSource.Token).GetAwaiter().GetResult();
                    }
                }
            }
        }
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(null);
            }
        }
    }
}
