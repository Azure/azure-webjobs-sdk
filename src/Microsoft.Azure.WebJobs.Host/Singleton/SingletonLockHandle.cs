// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Azure.WebJobs.Host
{
    internal class SingletonLockHandle : IDisposable
    {
        private TraceWriter _trace;
        private IDelayStrategy _speedupStrategy;
        private Func<Task> _leaseConflictCallbackAsync;
        private System.Timers.Timer _leaseRenewalTimer;
        private ISingletonRenewalMonitor _renewalMonitor;

        // For testing
        internal SingletonLockHandle()
        {
        }

        public SingletonLockHandle(IStorageBlockBlob blob, string leaseId, string lockId, IDelayStrategy speedupStrategy,
            Func<Task> leaseConflictCallbackAsync, ISingletonRenewalMonitor renewalMonitor, TraceWriter trace)
        {
            Blob = blob;
            LeaseId = leaseId;
            LockId = lockId;

            _leaseConflictCallbackAsync = leaseConflictCallbackAsync;
            _speedupStrategy = speedupStrategy;
            _trace = trace;
            _renewalMonitor = renewalMonitor;

            _leaseRenewalTimer = new System.Timers.Timer();
            _leaseRenewalTimer.AutoReset = false;
            _leaseRenewalTimer.Elapsed += OnRenewalTimer;
        }

        public string LeaseId { get; private set; }
        public string LockId { get; private set; }
        public IStorageBlockBlob Blob { get; private set; }
        public DateTime LastRenewalTime { get; private set; }
        public double LastRenewalInterval { get; private set; }

        public void StartTimer()
        {
            _leaseRenewalTimer.Interval = _speedupStrategy.GetNextDelay(true).TotalMilliseconds;
            _leaseRenewalTimer.Start();
        }

        public async void OnRenewalTimer(object sender, ElapsedEventArgs args)
        {
            await RenewLeaseAsync();
        }

        private async Task RenewLeaseAsync()
        {
            double nextIntervalInMs = await RenewalTimerElapsedAsync(CancellationToken.None);

            if (nextIntervalInMs == Timeout.Infinite)
            {
                if (_leaseConflictCallbackAsync != null)
                {
                    await _leaseConflictCallbackAsync();
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            else
            {
                _leaseRenewalTimer.Interval = nextIntervalInMs;
                _leaseRenewalTimer.Start();
            }
        }

        public virtual void StopTimer()
        {
            if (_leaseRenewalTimer != null)
            {
                _leaseRenewalTimer.Stop();
            }
        }

        internal async Task<double> RenewalTimerElapsedAsync(CancellationToken cancellationToken)
        {
            double nextIntervalInMs = 0;

            try
            {
                AccessCondition condition = new AccessCondition
                {
                    LeaseId = LeaseId
                };

                await Blob.RenewLeaseAsync(condition, null, null, cancellationToken);
                LastRenewalTime = DateTime.UtcNow;

                // The next execution should occur after a normal delay.
                nextIntervalInMs = _speedupStrategy.GetNextDelay(executionSucceeded: true).TotalMilliseconds;

                if (_renewalMonitor != null)
                {
                    _renewalMonitor.OnRenewal(LastRenewalTime, nextIntervalInMs);
                }
            }
            catch (StorageException exception)
            {
                if (exception.IsServerSideError())
                {
                    // The next execution should occur more quickly (try to renew the lease before it expires).
                    nextIntervalInMs = _speedupStrategy.GetNextDelay(executionSucceeded: false).TotalMilliseconds;
                    _trace.Warning(string.Format(CultureInfo.InvariantCulture, "Singleton lock renewal failed with error code {0}{1}. Retry renewal in {2} milliseconds.",
                        exception.RequestInformation.HttpStatusCode, FormatErrorCode(exception), nextIntervalInMs));
                }
                else if (exception.IsConflictLeaseIdMismatchWithLeaseOperation())
                {
                    nextIntervalInMs = Timeout.Infinite;
                    _trace.Error(string.Format(CultureInfo.InvariantCulture, "Singleton lock renewal failed with error code {0}{1}.",
                        exception.RequestInformation.HttpStatusCode, FormatErrorCode(exception), nextIntervalInMs));
                }
                else
                {
                    // Something unexpected happened and we likely cannot recover gracefully.
                    throw;
                }
            }

            LastRenewalInterval = nextIntervalInMs;

            return nextIntervalInMs;
        }

        private static string FormatErrorCode(StorageException exception)
        {
            string errorCode = exception.GetErrorCode();

            if (errorCode == null)
            {
                return string.Empty;
            }

            return ": " + errorCode;
        }

        public void Dispose()
        {
            _leaseRenewalTimer.Dispose();
        }
    }
}