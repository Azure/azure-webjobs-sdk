// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Listeners
{
    internal sealed class BlobListener : IListener, IScaleMonitorProvider
    {
        private readonly ISharedListener _sharedListener;
        private readonly ILogger<BlobListener> _logger;

        private bool _started;
        private bool _disposed;
        private Lazy<string> _details;

        // for mock test purposes only
        internal BlobListener(ISharedListener sharedListener)
        {
        }

        public BlobListener(ISharedListener sharedListener, CloudBlobContainer container, ILoggerFactory loggerFactory)
        {
            _sharedListener = sharedListener;
            _details = new Lazy<string>(() => $"blob container={container.Name}, storage account name={container.ServiceClient.GetAccountName()}");
            _logger = loggerFactory.CreateLogger<BlobListener>();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                ThrowIfDisposed();

                if (_started)
                {
                    throw new InvalidOperationException("The listener has already been started.");
                }

                _logger.LogDebug($"Storage blob listener started ({_details.Value})");
                return StartAsyncCore(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Storage blob listener exception during starting ({_details.Value})");
                throw;
            }
        }

        private async Task StartAsyncCore(CancellationToken cancellationToken)
        {
            // Starts the entire shared listener (if not yet started).
            // There is currently no scenario for controlling a single blob listener independently.
            await _sharedListener.EnsureAllStartedAsync(cancellationToken);
            _started = true;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                ThrowIfDisposed();

                if (!_started)
                {
                    throw new InvalidOperationException(
                        "The listener has not yet been started or has already been stopped.");
                }

                return StopAsyncCore(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"storage blob listener exception during stopping ({_details.Value})");
                throw;
            }
            finally
            {
                _logger.LogDebug($"Storage blob listener stopped ({_details.Value})");
            }
        }

        private async Task StopAsyncCore(CancellationToken cancellationToken)
        {
            // Stops the entire shared listener (if not yet stopped).
            // There is currently no scenario for controlling a single blob listener independently.
            await _sharedListener.EnsureAllStoppedAsync(cancellationToken);
            _started = false;
        }

        public void Cancel()
        {
            ThrowIfDisposed();
            _sharedListener.EnsureAllCanceled();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Disposes the entire shared listener (if not yet disposed).
                // There is currently no scenario for controlling a single blob listener independently.
                _sharedListener.EnsureAllDisposed();

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

        public IScaleMonitor GetMonitor()
        {
            // BlobListener is special - it uses a single shared QueueListener for all BlobTrigger functions,
            // so we must return that single shared instance.
            // Each individual BlobTrigger function will have its own listener, each pointing to the single
            // shared QueueListener. If all BlobTrigger functions are disabled, their listeners won't be created
            // so the shared queue won't be monitored.
            return ((IScaleMonitorProvider)_sharedListener).GetMonitor();
        }
    }
}
