// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Listeners
{
    internal sealed class ScanBlobScanLogHybridPollingStrategy : IBlobListenerStrategy
    {
        private static readonly TimeSpan _pollintInterval = TimeSpan.FromSeconds(10);
        private enum PollStrategy { ScanBlobs, ScanLogs, NotDetermined };
        private int _scanBlobLimitPerPoll = 10000;
        private readonly IDictionary<IStorageBlobContainer, ICollection<ITriggerExecutor<IStorageBlob>>> _registrations;
        private readonly IDictionary<IStorageBlobContainer, DateTime> _lastSweepCycleStartTime;
        private readonly IDictionary<IStorageBlobContainer, DateTime> _currentSweepCycleStartTime;
        private readonly ConcurrentQueue<IStorageBlob> _blobsFoundFromScanOrNotification;
        private readonly IDictionary<IStorageBlobContainer, BlobContinuationToken> _continuationTokens;
        private PollLogsStrategy _pollLogStrategy;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _disposed;

        public ScanBlobScanLogHybridPollingStrategy() : base()
        {
            _registrations = new Dictionary<IStorageBlobContainer, ICollection<ITriggerExecutor<IStorageBlob>>>(
                new StorageBlobContainerComparer());
            _pollLogStrategy = new PollLogsStrategy();
            _cancellationTokenSource = new CancellationTokenSource();
            _continuationTokens = new Dictionary<IStorageBlobContainer, BlobContinuationToken>();
            _lastSweepCycleStartTime = new Dictionary<IStorageBlobContainer, DateTime>(
                new StorageBlobContainerComparer());
            _currentSweepCycleStartTime = new Dictionary<IStorageBlobContainer, DateTime>(
                new StorageBlobContainerComparer());
            _blobsFoundFromScanOrNotification = new ConcurrentQueue<IStorageBlob>();
        }

        public void Start()
        {
            ThrowIfDisposed();
            _pollLogStrategy.Start();
        }

        public void Cancel()
        {
            ThrowIfDisposed();
            _pollLogStrategy.Cancel();
            _cancellationTokenSource.Cancel();
        }

        public async Task RegisterAsync(IStorageBlobContainer container, ITriggerExecutor<IStorageBlob> triggerExecutor, CancellationToken cancellationToken)
        {
            // Register and Execute are not concurrency-safe.
            // Avoiding calling Register while Execute is running is the caller's responsibility.
            ThrowIfDisposed();

            // Register all in logPolling, there is no problem if we get 2 notifications of the new blob
            Task registerLogPolling = _pollLogStrategy.RegisterAsync(container, triggerExecutor, cancellationToken);

            ICollection<ITriggerExecutor<IStorageBlob>> containerRegistrations;
            if (!_registrations.TryGetValue(container, out containerRegistrations))
            {
                containerRegistrations = new List<ITriggerExecutor<IStorageBlob>>();
                _registrations.Add(container, containerRegistrations);
            }

            containerRegistrations.Add(triggerExecutor);

            if (!_lastSweepCycleStartTime.ContainsKey(container))
            {
                _lastSweepCycleStartTime.Add(container, DateTime.MinValue);
            }

            if (!_currentSweepCycleStartTime.ContainsKey(container))
            {
                _currentSweepCycleStartTime.Add(container, DateTime.MinValue);
            }

            if (!_continuationTokens.ContainsKey(container))
            {
                _continuationTokens.Add(container, null);
            }
            await registerLogPolling;
        }

        public async Task<TaskSeriesCommandResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            Task LogPollingTask = _pollLogStrategy.ExecuteAsync(cancellationToken);
            List<IStorageBlob> failedNotifications = new List<IStorageBlob>();
            List<Task> notifications = new List<Task>();

            // Drain the background queue of blob written notifications.
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IStorageBlob blob;

                if (!_blobsFoundFromScanOrNotification.TryDequeue(out blob))
                {
                    break;
                }

                notifications.Add(NotifyRegistrationsAsync(blob, failedNotifications, cancellationToken));
            }
            Task.WhenAll(notifications).Wait();

            List<Task> pollingTasks = new List<Task>();
            foreach (IStorageBlobContainer container in _registrations.Keys)
            {
                pollingTasks.Add(PollAndNotify(container, cancellationToken, failedNotifications));
            }

            // Re-add any failed notifications for the next iteration.
            foreach (IStorageBlob failedNotification in failedNotifications)
            {
                _blobsFoundFromScanOrNotification.Enqueue(failedNotification);
            }

            await LogPollingTask;
            await Task.WhenAll(pollingTasks);

            // Run subsequent iterations at "_pollingInterval" second intervals.
            return new TaskSeriesCommandResult(wait: Task.Delay(_pollintInterval));
        }

        private async Task PollAndNotify(IStorageBlobContainer container, CancellationToken cancellationToken, List<IStorageBlob> failedNotifications)
        {

            cancellationToken.ThrowIfCancellationRequested();
            IEnumerable<IStorageBlob> newBlobs = await PollNewBlobsAsync(container, cancellationToken);

            foreach (IStorageBlob newBlob in newBlobs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await NotifyRegistrationsAsync(newBlob, failedNotifications, cancellationToken);
            }
        }

        public void Notify(IStorageBlob blobWritten)
        {
            ThrowIfDisposed();
            _blobsFoundFromScanOrNotification.Enqueue(blobWritten);
            _pollLogStrategy.Notify(blobWritten);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _pollLogStrategy.Dispose();
                _cancellationTokenSource.Dispose();
                _disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(null);
            }
        }

        public async Task<IEnumerable<IStorageBlob>> PollNewBlobsAsync(
            IStorageBlobContainer container, CancellationToken cancellationToken)
        {
            IEnumerable<IStorageListBlobItem> currentBlobs;
            IStorageBlobResultSegment blobSegment;
            int blobPollLimitPerContainer = _scanBlobLimitPerPoll / _registrations.Count;
            BlobContinuationToken continuationToken = _continuationTokens[container];

            // if starting the cycle, keep the current time stamp to be used as curser
            if (continuationToken == null)
            {
                _currentSweepCycleStartTime[container] = DateTime.UtcNow;
            }
            try
            {
                blobSegment = await container.ListBlobsSegmentedAsync(prefix: null, useFlatBlobListing: true, 
                    blobListingDetails: BlobListingDetails.None, maxResults: blobPollLimitPerContainer, currentToken: continuationToken, 
                    options: null, operationContext: null, cancellationToken: cancellationToken);
                currentBlobs = blobSegment.Results;
            }
            catch (StorageException exception)
            {
                if (exception.IsNotFound())
                {
                    return Enumerable.Empty<IStorageBlob>();
                }
                else
                {
                    throw;
                }
            }

            List<IStorageBlob> newBlobs = new List<IStorageBlob>();

            // Type cast to IStorageBlob is safe due to useFlatBlobListing: true above.
            foreach (IStorageBlob currentBlob in currentBlobs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IStorageBlobProperties properties = currentBlob.Properties;
                DateTime lastModifiedTimestamp = properties.LastModified.Value.UtcDateTime;

                if (lastModifiedTimestamp > _lastSweepCycleStartTime[container])
                {
                    newBlobs.Add(currentBlob);
                }
            }

            // record continuation token for next chunk retrieval
            _continuationTokens[container] = blobSegment.ContinuationToken;

            // if ending a cycle then copy currentSweepCycleStartTime to lastSweepCycleStartTime
            if (blobSegment.ContinuationToken == null)
            {
                _lastSweepCycleStartTime[container] = _currentSweepCycleStartTime[container];
            }

            return newBlobs;
        }

        private async Task NotifyRegistrationsAsync(IStorageBlob blob, ICollection<IStorageBlob> failedNotifications,
            CancellationToken cancellationToken)
        {
            IStorageBlobContainer container = blob.Container;
            ICollection<ITriggerExecutor<IStorageBlob>> registrations;

            // Blob written notifications are host-wide, so filter out things that aren't in the container list.
            if (!_registrations.TryGetValue(container, out registrations))
            {
                return;
            }

            foreach (ITriggerExecutor<IStorageBlob> registration in registrations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                FunctionResult result = await registration.ExecuteAsync(blob, cancellationToken);
                if (!result.Succeeded)
                {
                    // If notification failed, try again on the next iteration.
                    failedNotifications.Add(blob);
                }
            }
        }
    }
}
