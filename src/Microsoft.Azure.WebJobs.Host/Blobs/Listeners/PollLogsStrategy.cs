﻿// Copyright (c) .NET Foundation. All rights reserved.
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
using ThreadState = System.Threading.ThreadState;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Listeners
{
    // A hybrid strategy that begins with a full container scan and then does incremental updates via log polling.
    internal sealed class PollLogsStrategy : IBlobListenerStrategy
    {
        private static readonly TimeSpan TwoSeconds = TimeSpan.FromSeconds(2);

        private readonly IDictionary<IStorageBlobContainer, ICollection<ITriggerExecutor<IStorageBlob>>> _registrations;
        private readonly IDictionary<IStorageBlobClient, BlobLogListener> _logListeners;
        private readonly Thread _initialScanThread;
        private readonly ConcurrentQueue<IStorageBlob> _blobsFoundFromScanOrNotification;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly TraceWriter _trace;
        private readonly Stopwatch _stopwatch;
        private bool _performInitialScan;
        private bool _disposed;

        public PollLogsStrategy(bool performInitialScan = true, TraceWriter trace = null)
        {
            _registrations = new Dictionary<IStorageBlobContainer, ICollection<ITriggerExecutor<IStorageBlob>>>(
                new StorageBlobContainerComparer());
            _logListeners = new Dictionary<IStorageBlobClient, BlobLogListener>(new StorageBlobClientComparer());
            _initialScanThread = new Thread(ScanContainers);
            _blobsFoundFromScanOrNotification = new ConcurrentQueue<IStorageBlob>();
            _cancellationTokenSource = new CancellationTokenSource();
            _performInitialScan = performInitialScan;
            _trace = trace;
            _stopwatch = new Stopwatch();
        }

        public async Task RegisterAsync(IStorageBlobContainer container, ITriggerExecutor<IStorageBlob> triggerExecutor,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            // Initial background scans for all containers happen on first Execute call.
            // Prevent accidental late registrations.
            // (Also prevents incorrect concurrent execution of Register with Execute.)
            if (_initialScanThread.ThreadState != ThreadState.Unstarted)
            {
                throw new InvalidOperationException("All registrations must be created before execution begins.");
            }

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

            IStorageBlobClient client = container.ServiceClient;

            if (!_logListeners.ContainsKey(client))
            {
                BlobLogListener logListener = await BlobLogListener.CreateAsync(client, cancellationToken);
                _logListeners.Add(client, logListener);
            }
        }

        public void Notify(IStorageBlob blobWritten)
        {
            ThrowIfDisposed();
            _blobsFoundFromScanOrNotification.Enqueue(blobWritten);
        }

        public async Task<TaskSeriesCommandResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            ConcurrentBag<IStorageBlob> failedNotifications = new ConcurrentBag<IStorageBlob>(); // NotifyRegistrationAsync is concurrent
            List<Task> notifications = new List<Task>();

            // Drain the background queue (for initial container scans and blob written notifications).
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
            notifications.Clear();

            _stopwatch.Restart();
            // Poll the logs (to detect ongoing writes).
            foreach (BlobLogListener logListener in _logListeners.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (IStorageBlob blob in await logListener.GetRecentBlobWritesAsync(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    notifications.Add(NotifyRegistrationsAsync(blob, failedNotifications, cancellationToken)); // concurrent
                }
            }

            _stopwatch.Stop();
            if (_trace != null)
            {
                _trace.Verbose($"scan {_logListeners.Count} storage logs and enqueue finished in {_stopwatch.ElapsedMilliseconds}ms");
            }

            // Re-add any failed notifications for the next iteration.
            foreach (IStorageBlob failedNotification in failedNotifications)
            {
                _blobsFoundFromScanOrNotification.Enqueue(failedNotification);
            }

            // Run subsequent iterations at 2 second intervals.
            return new TaskSeriesCommandResult(wait: Task.Delay(TwoSeconds));
        }

        public void Start()
        {
            ThrowIfDisposed();

            // Start a background scan of the container on first execution. Later writes will be found via polling logs.
            // Thread monitors _cancellationTokenSource.Token (that's the only way this thread is controlled).
            if (_performInitialScan)
            {
                _initialScanThread.Start(_cancellationTokenSource.Token);
            }
        }

        public void Cancel()
        {
            ThrowIfDisposed();
            _cancellationTokenSource.Cancel();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cancellationTokenSource.Dispose();
                _disposed = true;
            }
        }

        private async Task NotifyRegistrationsAsync(IStorageBlob blob, ConcurrentBag<IStorageBlob> failedNotifications, CancellationToken cancellationToken)
        {
            IStorageBlobContainer container = blob.Container;

            // Log listening is client-wide and blob written notifications are host-wide, so filter out things that
            // aren't in the container list.
            if (!_registrations.ContainsKey(container))
            {
                return;
            }

            bool success = true;
            foreach (ITriggerExecutor<IStorageBlob> registration in _registrations[container])
            {
                cancellationToken.ThrowIfCancellationRequested();

                FunctionResult result = await registration.ExecuteAsync(blob, cancellationToken);
                if (!result.Succeeded)
                {
                    success = false;
                }
            }

            if (!success)
            {
                // If notification failed, try again on the next iteration.
                _blobsFoundFromScanOrNotification.Enqueue(blob);
            }
        }

        private void ScanContainers(object state)
        {
            CancellationToken cancellationToken = (CancellationToken)state;

            foreach (IStorageBlobContainer container in _registrations.Keys)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                List<IStorageListBlobItem> items;

                try
                {
                    // Non-async is correct here. ScanContainers occurs on a background thread. Unless it blocks, no one
                    // else is around to observe the results.
                    items = container.ListBlobsAsync(prefix: null, useFlatBlobListing: true,
                        cancellationToken: CancellationToken.None).GetAwaiter().GetResult().ToList();
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

                // Type cast to IStorageBlob is safe due to useFlatBlobListing: true above.
                foreach (IStorageBlob item in items)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    _blobsFoundFromScanOrNotification.Enqueue(item);
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
