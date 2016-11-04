﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.Timers;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Listeners
{
    internal sealed class SharedBlobListener : ISharedListener
    {
        private readonly IWebJobsExceptionHandler _exceptionHandler;
        private readonly IBlobListenerStrategy _strategy;
        private ITaskSeriesTimer _timer;

        private bool _started;
        private bool _disposed;

        public SharedBlobListener(string hostId, IStorageAccount storageAccount,
            IWebJobsExceptionHandler exceptionHandler)
        {
            _exceptionHandler = exceptionHandler;
            _strategy = CreateStrategy(hostId, storageAccount);
        }

        public IBlobWrittenWatcher BlobWritterWatcher
        {
            get { return _strategy; }
        }

        public Task RegisterAsync(IStorageBlobContainer container, ITriggerExecutor<IStorageBlob> triggerExecutor,
            CancellationToken cancellationToken)
        {
            if (_started)
            {
                throw new InvalidOperationException(
                    "Registrations may not be added while the shared listener is running.");
            }

            return _strategy.RegisterAsync(container, triggerExecutor, cancellationToken);
        }

        public Task EnsureAllStartedAsync(CancellationToken cancellationToken)
        {
            if (!_started)
            {
                // Dispose any existing timer, which can occur on a restart of the listener.
                if (_timer != null)
                {
                    _timer.Dispose();
                }

                // Start the first iteration immediately.
                _timer = new TaskSeriesTimer(_strategy, _exceptionHandler, initialWait: Task.Delay(0));

                _timer.Start();
                _strategy.Start();
                _started = true;
            }

            return Task.FromResult(0);
        }

        public async Task EnsureAllStoppedAsync(CancellationToken cancellationToken)
        {
            if (_started)
            {
                _strategy.Cancel();
                await _timer.StopAsync(cancellationToken);
                _started = false;
            }
        }

        public void EnsureAllCanceled()
        {
            _strategy.Cancel();
            _timer.Cancel();
        }

        public void EnsureAllDisposed()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _strategy.Dispose();
                _timer.Dispose();
                _disposed = true;
            }
        }

        private static IBlobListenerStrategy CreateStrategy(string hostId, IStorageAccount account)
        {
            if (!StorageClient.IsDevelopmentStorageAccount(account))
            {
                IBlobScanInfoManager scanInfoManager = new StorageBlobScanInfoManager(hostId, account.CreateBlobClient());
                return new ScanBlobScanLogHybridPollingStrategy(scanInfoManager);
            }
            else
            {
                return new ScanContainersStrategy();
            }
        }
    }
}
