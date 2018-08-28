// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
        private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(10);
        private readonly IDictionary<IStorageBlobContainer, ContainerScanInfo> _scanInfo;
        private readonly ConcurrentQueue<IStorageBlob> _blobsFoundFromScanOrNotification;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly TraceWriter _trace;
        private readonly Stopwatch _stopwatch;
        private IBlobScanInfoManager _blobScanInfoManager;
        // A budget is allocated representing the number of blobs to be listed in a polling 
        // interval, each container will get its share of _scanBlobLimitPerPoll/number of containers.
        // this share will be listed for each container each polling interval
        private int _scanBlobLimitPerPoll = 10000;
        private PollLogsStrategy _pollLogStrategy;
        private bool _disposed;

        public ScanBlobScanLogHybridPollingStrategy(IBlobScanInfoManager blobScanInfoManager, TraceWriter trace = null) : base()
        {
            _blobScanInfoManager = blobScanInfoManager;
            _trace = trace;
            _scanInfo = new Dictionary<IStorageBlobContainer, ContainerScanInfo>(new StorageBlobContainerComparer());
            _pollLogStrategy = new PollLogsStrategy(performInitialScan: false, trace: trace);
            _cancellationTokenSource = new CancellationTokenSource();
            _blobsFoundFromScanOrNotification = new ConcurrentQueue<IStorageBlob>();
            _stopwatch = new Stopwatch();
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
            await _pollLogStrategy.RegisterAsync(container, triggerExecutor, cancellationToken);

            ContainerScanInfo containerScanInfo;
            if (!_scanInfo.TryGetValue(container, out containerScanInfo))
            {
                // First, try to load serialized scanInfo for this container.
                DateTime? latestStoredScan = await _blobScanInfoManager.LoadLatestScanAsync(container.ServiceClient.Credentials.AccountName, container.Name);

                containerScanInfo = new ContainerScanInfo()
                {
                    Registrations = new List<ITriggerExecutor<IStorageBlob>>(),
                    LastSweepCycleLatestModified = latestStoredScan ?? DateTime.MinValue,
                    CurrentSweepCycleLatestModified = DateTime.MinValue,
                    ContinuationToken = null
                };

                _scanInfo.Add(container, containerScanInfo);
            }

            containerScanInfo.Registrations.Add(triggerExecutor);
        }

        public async Task<TaskSeriesCommandResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            Task logPollingTask = _pollLogStrategy.ExecuteAsync(cancellationToken);
            ConcurrentBag<IStorageBlob> failedNotifications = new ConcurrentBag<IStorageBlob>(); // NotifyRegistrationAsync is concurrent
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
            await Task.WhenAll(notifications);

            _stopwatch.Restart();
            // For each container, scan blobs
            foreach (KeyValuePair<IStorageBlobContainer, ContainerScanInfo> containerScanInfoPair in _scanInfo)
            {
                await PollAndNotifyAsync(containerScanInfoPair.Key, containerScanInfoPair.Value, cancellationToken, failedNotifications);
            }
            _stopwatch.Stop();
            // performance trace
            if (_trace != null)
            {
                _trace.Verbose($"scan {_scanInfo.Count} containers and enqueue finished in {_stopwatch.ElapsedMilliseconds}ms");
            }

            // Re-add any failed notifications for the next iteration.
            foreach (IStorageBlob failedNotification in failedNotifications)
            {
                _blobsFoundFromScanOrNotification.Enqueue(failedNotification);
            }

            await logPollingTask;

            // Run subsequent iterations at "_pollingInterval" second intervals.
            return new TaskSeriesCommandResult(wait: Task.Delay(PollingInterval));
        }

        private async Task PollAndNotifyAsync(IStorageBlobContainer container, ContainerScanInfo containerScanInfo, CancellationToken cancellationToken, ConcurrentBag<IStorageBlob> failedNotifications)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateTime lastScan = containerScanInfo.LastSweepCycleLatestModified;
            IEnumerable<IStorageBlob> newBlobs = await PollNewBlobsAsync(container, containerScanInfo, cancellationToken);
            List<Task> notifications = new List<Task>();

            foreach (IStorageBlob newBlob in newBlobs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                notifications.Add(NotifyRegistrationsAsync(newBlob, failedNotifications, cancellationToken));
            }

            await Task.WhenAll(notifications);

            // if the 'LatestModified' has changed, update it in the manager
            if (containerScanInfo.LastSweepCycleLatestModified > lastScan)
            {
                DateTime latestScan = containerScanInfo.LastSweepCycleLatestModified;

                // It's possible that we had some blobs that we failed to move to the queue. We want to make sure
                // we continue to find these if the host needs to restart.
                if (failedNotifications.Any())
                {
                    latestScan = failedNotifications.Min(n => n.Properties.LastModified.Value.UtcDateTime);
                }

                // Store our timestamp slightly earlier than the last timestamp. This is a failsafe for any blobs that created 
                // milliseconds after our last scan (blob timestamps round to the second). This way we make sure to pick those
                // up on a host restart.
                await _blobScanInfoManager.UpdateLatestScanAsync(container.ServiceClient.Credentials.AccountName,
                    container.Name, latestScan.AddMilliseconds(-1));
            }
        }

        public void Notify(IStorageBlob blobWritten)
        {
            ThrowIfDisposed();
            _blobsFoundFromScanOrNotification.Enqueue(blobWritten);
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

        /// <summary>
        /// This method is called each polling interval for all containers. The method divides the 
        /// budget of allocated number of blobs to query, for each container we query a page of 
        /// that size and we keep the continuation token for the next time. AS a curser, we use
        /// the time stamp when the current cycle on the container started. blobs newer than that
        /// time will be considered new and registrations will be notified
        /// </summary>
        /// <param name="container"></param>
        /// <param name="containerScanInfo"> Information that includes the last cycle start
        /// the continuation token and the current cycle start for a container</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IEnumerable<IStorageBlob>> PollNewBlobsAsync(
            IStorageBlobContainer container, ContainerScanInfo containerScanInfo, CancellationToken cancellationToken)
        {
            IEnumerable<IStorageListBlobItem> currentBlobs;
            IStorageBlobResultSegment blobSegment;
            int blobPollLimitPerContainer = _scanBlobLimitPerPoll / _scanInfo.Count;
            BlobContinuationToken continuationToken = containerScanInfo.ContinuationToken;

            // if starting the cycle, reset the sweep time
            if (continuationToken == null)
            {
                containerScanInfo.CurrentSweepCycleLatestModified = DateTime.MinValue;
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

                if (lastModifiedTimestamp > containerScanInfo.CurrentSweepCycleLatestModified)
                {
                    containerScanInfo.CurrentSweepCycleLatestModified = lastModifiedTimestamp;
                }

                if (lastModifiedTimestamp > containerScanInfo.LastSweepCycleLatestModified)
                {
                    newBlobs.Add(currentBlob);
                }
            }

            // record continuation token for next chunk retrieval
            containerScanInfo.ContinuationToken = blobSegment.ContinuationToken;

            // if ending a cycle then copy currentSweepCycleStartTime to lastSweepCycleStartTime, if changed
            if (blobSegment.ContinuationToken == null &&
                containerScanInfo.CurrentSweepCycleLatestModified > containerScanInfo.LastSweepCycleLatestModified)
            {
                containerScanInfo.LastSweepCycleLatestModified = containerScanInfo.CurrentSweepCycleLatestModified;
            }

            return newBlobs;
        }

        // multithread
        private async Task NotifyRegistrationsAsync(IStorageBlob blob, ConcurrentBag<IStorageBlob> failedNotifications, CancellationToken cancellationToken)
        {
            IStorageBlobContainer container = blob.Container;
            ContainerScanInfo containerScanInfo;

            // Blob written notifications are host-wide, so filter out things that aren't in the container list.
            if (!_scanInfo.TryGetValue(container, out containerScanInfo))
            {
                return;
            }

            bool success = true;
            foreach (ITriggerExecutor<IStorageBlob> registration in containerScanInfo.Registrations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                FunctionResult result = await registration.ExecuteAsync(blob, cancellationToken);
                if (!result.Succeeded)
                {
                    // failed for blob and one of its triggerFunction
                    success = false;
                }
            }

            if (!success)
            {
                // If notification failed, try again on the next iteration.
                // which will try to enqueue for all registration of the single blob
                failedNotifications.Add(blob);
            }
        }
    }
}