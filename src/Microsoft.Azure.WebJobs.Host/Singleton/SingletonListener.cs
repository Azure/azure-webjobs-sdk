// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Azure.WebJobs.Host.Timers;

namespace Microsoft.Azure.WebJobs.Host.Listeners
{
    internal class SingletonListener : IListener
    {
        private readonly SingletonAttribute _attribute;
        private readonly SingletonManager _singletonManager;
        private readonly SingletonConfiguration _singletonConfig;
        private readonly IListener _innerListener;
        private readonly TraceWriter _trace;
        private readonly IWebJobsExceptionHandler _exceptionHandler;
        private string _lockId;
        private object _lockHandle;
        private bool _isListening;
        private string _scopeId;

        public SingletonListener(MethodInfo method, SingletonAttribute attribute, SingletonManager singletonManager, IListener innerListener, IWebJobsExceptionHandler exceptionHandler, TraceWriter trace)
        {
            _attribute = attribute;
            _singletonManager = singletonManager;
            _singletonConfig = _singletonManager.Config;
            _innerListener = innerListener;
            _trace = trace;
            _exceptionHandler = exceptionHandler;

            string boundScopeId = _singletonManager.GetBoundScopeId(_attribute.ScopeId);
            _lockId = singletonManager.FormatLockId(method, _attribute.Scope, boundScopeId);
            _lockId += ".Listener";

            // used for logging
            _scopeId = FormatScopeId(method, boundScopeId);
        }

        // exposed for testing
        internal System.Timers.Timer LockTimer { get; set; }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // When recovery is enabled, we don't do retries on the individual lock attempts,
            // since retries are being done outside
            bool recoveryEnabled = _singletonConfig.ListenerLockRecoveryPollingInterval != TimeSpan.MaxValue;
            _lockHandle = await _singletonManager.TryLockAsync(_lockId, null, _attribute, _innerListener as ISingletonRenewalMonitor, OnRenewalFailureAsync,
                cancellationToken, retry: !recoveryEnabled);

            if (_lockHandle == null)
            {
                // If we're unable to acquire the lock, it means another listener
                // has it so we return w/o starting our listener.
                //
                // However, we also start a periodic background "recovery" timer that will recheck
                // occasionally for the lock. This ensures that if the host that has the lock goes
                // down for whatever reason, others will have a chance to resume the work.
                if (recoveryEnabled)
                {
                    LockTimer = new System.Timers.Timer(_singletonConfig.ListenerLockRecoveryPollingInterval.TotalMilliseconds);
                    LockTimer.Elapsed += OnLockTimer;
                    LockTimer.Start();
                }
                return;
            }

            _trace.Verbose(FormatStartStopMessage(true));
            await _innerListener.StartAsync(cancellationToken);

            _isListening = true;
        }

        internal async Task OnRenewalFailureAsync()
        {
            _trace.Verbose(string.Format(CultureInfo.InvariantCulture, "Lock renewal failed for '{0}'. Restarting SingletonListener.", _lockId));
            await StopAsync(CancellationToken.None);
            await StartAsync(CancellationToken.None);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (LockTimer != null)
            {
                LockTimer.Stop();
            }

            await ReleaseLockAsync(cancellationToken);

            if (_isListening)
            {
                _trace.Verbose(FormatStartStopMessage(false));
                await _innerListener.StopAsync(cancellationToken);
                _isListening = false;
            }
        }

        public void Cancel()
        {
            if (LockTimer != null)
            {
                LockTimer.Stop();
            }

            _innerListener.Cancel();
        }

        public void Dispose()
        {
            if (LockTimer != null)
            {
                LockTimer.Dispose();
            }

            // When we Dispose, it's important that we release the lock if we
            // have it.
            ReleaseLockAsync().GetAwaiter().GetResult();

            _innerListener.Dispose();
        }

        private void OnLockTimer(object sender, ElapsedEventArgs e)
        {
            try
            {
                TryAcquireLock().GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                // If something unexpected happens here, bring down the host so everything restarts.
                ExceptionDispatchInfo exceptionInfo = ExceptionDispatchInfo.Capture(exception);
                _exceptionHandler.OnUnhandledExceptionAsync(exceptionInfo).GetAwaiter().GetResult();
            }
        }

        internal async Task TryAcquireLock()
        {
            _lockHandle = await _singletonManager.TryLockAsync(_lockId, null, _attribute, _innerListener as ISingletonRenewalMonitor,
                OnRenewalFailureAsync, CancellationToken.None, retry: false);

            if (_lockHandle != null)
            {
                if (LockTimer != null)
                {
                    LockTimer.Stop();
                    LockTimer.Dispose();
                    LockTimer = null;
                }

                _trace.Verbose(FormatStartStopMessage(true));
                await _innerListener.StartAsync(CancellationToken.None);

                _isListening = true;
            }
        }

        private async Task ReleaseLockAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (_lockHandle != null)
            {
                await _singletonManager.ReleaseLockAsync(_lockHandle, cancellationToken);
                _lockHandle = null;
            }
        }

        // Generate an identifier for logging. This helps us track which listeners are starting/stopping.
        // The logic here is similar to what SingletonManager does to create a lock id, but we want to keep this independent
        // of any changes there.
        private static string FormatScopeId(MethodInfo method, string boundScopeId)
        {
            string scopeId = string.Empty;

            if (method != null)
            {
                scopeId = method.DeclaringType.FullName + "." + method.Name;
            }

            if (!string.IsNullOrEmpty(boundScopeId))
            {
                if (!string.IsNullOrEmpty(scopeId))
                {
                    scopeId += ".";
                }
                scopeId += boundScopeId;
            }

            return scopeId;
        }

        private string FormatStartStopMessage(bool isStart)
        {
            return string.Format(CultureInfo.InvariantCulture, "SingletonListener {0} inner listener of type '{1}' with scope '{2}'",
                isStart ? "starting" : "stopping",
                _innerListener.GetType().FullName,
                _scopeId);
        }
    }
}