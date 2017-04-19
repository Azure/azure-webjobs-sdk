// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Lease;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Encapsulates and manages blob leases for Singleton locks.
    /// </summary>
    internal class SingletonManager
    {
        internal const string FunctionInstanceMetadataKey = "FunctionInstance";
        private readonly INameResolver _nameResolver;
        private readonly IWebJobsExceptionHandler _exceptionHandler;
        private readonly SingletonConfiguration _config;
        private TimeSpan _minimumLeaseRenewalInterval = TimeSpan.FromSeconds(1);
        private TraceWriter _trace;
        private IHostIdProvider _hostIdProvider;
        private string _hostId;
        private ILeaseProxy _leaseProxy;

        // For mock testing only
        internal SingletonManager()
        {
        }

        public SingletonManager(ILeaseProxy leaseProxy, IWebJobsExceptionHandler exceptionHandler, SingletonConfiguration config, TraceWriter trace, IHostIdProvider hostIdProvider, INameResolver nameResolver = null)
        {
            _leaseProxy = leaseProxy;
            _nameResolver = nameResolver;
            _exceptionHandler = exceptionHandler;
            _config = config;
            _trace = trace;
            _hostIdProvider = hostIdProvider;
        }

        internal virtual SingletonConfiguration Config
        {
            get
            {
                return _config;
            }
        }

        internal string HostId
        {
            get
            {
                if (_hostId == null)
                {
                    _hostId = _hostIdProvider.GetHostIdAsync(CancellationToken.None).Result;
                }
                return _hostId;
            }
        }

        // for testing
        internal TimeSpan MinimumLeaseRenewalInterval
        {
            get
            {
                return _minimumLeaseRenewalInterval;
            }
            set
            {
                _minimumLeaseRenewalInterval = value;
            }
        }

        public async virtual Task<object> LockAsync(string lockId, string functionInstanceId, SingletonAttribute attribute, CancellationToken cancellationToken)
        {
            object lockHandle = await TryLockAsync(lockId, functionInstanceId, attribute, cancellationToken);

            if (lockHandle == null)
            {
                TimeSpan acquisitionTimeout = attribute.LockAcquisitionTimeout != null
                    ? TimeSpan.FromSeconds(attribute.LockAcquisitionTimeout.Value) :
                    _config.LockAcquisitionTimeout;
                throw new TimeoutException(string.Format("Unable to acquire singleton lock blob lease for blob '{0}' (timeout of {1} exceeded).", lockId, acquisitionTimeout.ToString("g")));
            }

            return lockHandle;
        }

        public async virtual Task<object> TryLockAsync(string lockId, string functionInstanceId, SingletonAttribute attribute, CancellationToken cancellationToken, bool retry = true)
        {
            TimeSpan lockPeriod = GetLockPeriod(attribute, _config);
            var leaseDefinition = new LeaseDefinition
            {
                AccountName = GetAccountName(attribute),
                Namespaces = new List<string> { HostContainerNames.Hosts, HostDirectoryNames.SingletonLocks },
                Name = lockId,
                Period = lockPeriod
            };

            string leaseId = await _leaseProxy.TryAcquireLeaseAsync(leaseDefinition, cancellationToken);
 
            if (string.IsNullOrEmpty(leaseId) && retry)
            {
                // Someone else has the lease. Continue trying to periodically get the lease for
                // a period of time
                TimeSpan acquisitionTimeout = attribute.LockAcquisitionTimeout != null
                    ? TimeSpan.FromSeconds(attribute.LockAcquisitionTimeout.Value) :
                    _config.LockAcquisitionTimeout;

                TimeSpan timeWaited = TimeSpan.Zero;
                while (string.IsNullOrEmpty(leaseId) && (timeWaited < acquisitionTimeout))
                {
                    await Task.Delay(_config.LockAcquisitionPollingInterval);
                    timeWaited += _config.LockAcquisitionPollingInterval;
                    leaseId = await _leaseProxy.TryAcquireLeaseAsync(leaseDefinition, cancellationToken);
                }
            }

            if (string.IsNullOrEmpty(leaseId))
            {
                return null;
            }

            leaseDefinition.LeaseId = leaseId;

            _trace.Verbose(string.Format(CultureInfo.InvariantCulture, "Singleton lock acquired ({0})", lockId), source: TraceSource.Execution);

            if (!string.IsNullOrEmpty(functionInstanceId))
            {
                await _leaseProxy.WriteLeaseMetadataAsync(leaseDefinition, FunctionInstanceMetadataKey, functionInstanceId,
                    cancellationToken);
            }

            SingletonLockHandle lockHandle = new SingletonLockHandle
            {
                LeaseDefinition = leaseDefinition,
                LeaseRenewalTimer = CreateLeaseRenewalTimer(_leaseProxy, leaseDefinition, _exceptionHandler)
            };

            // start the renewal timer, which ensures that we maintain our lease until
            // the lock is released
            lockHandle.LeaseRenewalTimer.Start();

            return lockHandle;
        }

        public async virtual Task ReleaseLockAsync(object lockHandle, CancellationToken cancellationToken)
        {
            SingletonLockHandle singletonLockHandle = (SingletonLockHandle)lockHandle;

            if (singletonLockHandle.LeaseRenewalTimer != null)
            {
                await singletonLockHandle.LeaseRenewalTimer.StopAsync(cancellationToken);
            }

            await _leaseProxy.ReleaseLeaseAsync(singletonLockHandle.LeaseDefinition, cancellationToken);

            _trace.Verbose(string.Format(CultureInfo.InvariantCulture, "Singleton lock released ({0})", singletonLockHandle.LeaseDefinition.Name), source: TraceSource.Execution);
        }

        public string FormatLockId(MethodInfo method, SingletonScope scope, string scopeId)
        {
            return FormatLockId(method, scope, HostId, scopeId);
        }

        public static string FormatLockId(MethodInfo method, SingletonScope scope, string hostId, string scopeId)
        {
            if (string.IsNullOrEmpty(hostId))
            {
                throw new ArgumentNullException("hostId");
            }

            string lockId = string.Empty;
            if (scope == SingletonScope.Function)
            {
                lockId += string.Format(CultureInfo.InvariantCulture, "{0}.{1}", method.DeclaringType.FullName, method.Name);
            }

            if (!string.IsNullOrEmpty(scopeId))
            {
                if (!string.IsNullOrEmpty(lockId))
                {
                    lockId += ".";
                }
                lockId += scopeId;
            }

            lockId = string.Format(CultureInfo.InvariantCulture, "{0}/{1}", hostId, lockId);

            return lockId;
        }

        public string GetBoundScopeId(string scopeId, IReadOnlyDictionary<string, object> bindingData = null)
        {
            if (_nameResolver != null)
            {
                scopeId = _nameResolver.ResolveWholeString(scopeId);
            }

            if (bindingData != null)
            {
                BindingTemplate bindingTemplate = BindingTemplate.FromString(scopeId);
                IReadOnlyDictionary<string, string> parameters = BindingDataPathHelper.ConvertParameters(bindingData);
                return bindingTemplate.Bind(parameters);
            }
            else
            {
                return scopeId;
            }
        }

        public static SingletonAttribute GetFunctionSingletonOrNull(FunctionDescriptor descriptor, bool isTriggered)
        {
            if (!isTriggered && descriptor.SingletonAttributes.Any(p => p.Mode == SingletonMode.Listener))
            {
                throw new NotSupportedException("SingletonAttribute using mode 'Listener' cannot be applied to non-triggered functions.");
            }

            SingletonAttribute[] singletonAttributes = descriptor.SingletonAttributes.Where(p => p.Mode == SingletonMode.Function).ToArray();
            SingletonAttribute singletonAttribute = null;
            if (singletonAttributes.Length > 1)
            {
                throw new NotSupportedException("Only one SingletonAttribute using mode 'Function' is allowed.");
            }
            else if (singletonAttributes.Length == 1)
            {
                singletonAttribute = singletonAttributes[0];
                ValidateSingletonAttribute(singletonAttribute, SingletonMode.Function);
            }

            return singletonAttribute;
        }

        /// <summary>
        /// Creates and returns singleton listener scoped to the host.
        /// </summary>
        /// <param name="innerListener">The inner listener to wrap.</param>
        /// <param name="scopeId">The scope ID to use.</param>
        /// <returns>The singleton listener.</returns>
        public SingletonListener CreateHostSingletonListener(IListener innerListener, string scopeId)
        {
            SingletonAttribute singletonAttribute = new SingletonAttribute(scopeId, SingletonScope.Host)
            {
                Mode = SingletonMode.Listener
            };
            return new SingletonListener(null, singletonAttribute, this, innerListener, _trace);
        }

        public static SingletonAttribute GetListenerSingletonOrNull(Type listenerType, MethodInfo method)
        {
            // First check the method, then the listener class. This allows a method to override an implicit
            // listener singleton.
            SingletonAttribute singletonAttribute = null;
            SingletonAttribute[] singletonAttributes = method.GetCustomAttributes<SingletonAttribute>().Where(p => p.Mode == SingletonMode.Listener).ToArray();
            if (singletonAttributes.Length > 1)
            {
                throw new NotSupportedException("Only one SingletonAttribute using mode 'Listener' is allowed.");
            }
            else if (singletonAttributes.Length == 1)
            {
                singletonAttribute = singletonAttributes[0];
            }
            else
            {
                singletonAttribute = listenerType.GetCustomAttributes<SingletonAttribute>().SingleOrDefault(p => p.Mode == SingletonMode.Listener);
            }

            if (singletonAttribute != null)
            {
                ValidateSingletonAttribute(singletonAttribute, SingletonMode.Listener);
            }

            return singletonAttribute;
        }

        internal static void ValidateSingletonAttribute(SingletonAttribute attribute, SingletonMode mode)
        {
            if (attribute.Scope == SingletonScope.Host && string.IsNullOrEmpty(attribute.ScopeId))
            {
                throw new InvalidOperationException("A ScopeId value must be provided when using scope 'Host'.");
            }

            if (mode == SingletonMode.Listener && attribute.Scope == SingletonScope.Host)
            {
                throw new InvalidOperationException("Scope 'Host' cannot be used when the mode is set to 'Listener'.");
            }
        }

        public async virtual Task<string> GetLockOwnerAsync(SingletonAttribute attribute, string lockId, CancellationToken cancellationToken)
        {
            var leaseDefinition = new LeaseDefinition
            {
                AccountName = GetAccountName(attribute),
                Namespaces = new List<string> { HostContainerNames.Hosts, HostDirectoryNames.SingletonLocks },
                Name = lockId,
            };

            LeaseInformation leaseInfo = await _leaseProxy.ReadLeaseInfoAsync(leaseDefinition, cancellationToken);

            // if the lease is Available, then there is no current owner
            // (any existing owner value is the last owner that held the lease)
            if (leaseInfo.IsLeaseAvailable)
            {
                return null;
            }

            string owner = string.Empty;
            leaseInfo.Metadata.TryGetValue(FunctionInstanceMetadataKey, out owner);

            return owner;
        }

        internal static TimeSpan GetLockPeriod(SingletonAttribute attribute, SingletonConfiguration config)
        {
            return attribute.Mode == SingletonMode.Listener ?
                    config.ListenerLockPeriod : config.LockPeriod;
        }

        private ITaskSeriesTimer CreateLeaseRenewalTimer(ILeaseProxy leaseProxy, LeaseDefinition leaseDefinition, IWebJobsExceptionHandler exceptionHandler)
        {
            // renew the lease when it is halfway to expiring   
            TimeSpan normalUpdateInterval = new TimeSpan(leaseDefinition.Period.Ticks / 2);

            IDelayStrategy speedupStrategy = new LinearSpeedupStrategy(normalUpdateInterval, MinimumLeaseRenewalInterval);
            ITaskSeriesCommand command = new RenewLeaseCommand(leaseProxy, leaseDefinition, speedupStrategy, _trace, leaseDefinition.Period);
            return new TaskSeriesTimer(command, exceptionHandler, Task.Delay(normalUpdateInterval));
        }

        internal class SingletonLockHandle
        {
            public LeaseDefinition LeaseDefinition { get; set; }
            public ITaskSeriesTimer LeaseRenewalTimer { get; set; }
        }

        internal class RenewLeaseCommand : ITaskSeriesCommand
        {
            private readonly ILeaseProxy _leaseProxy;
            private readonly LeaseDefinition _leaseDefinition;
            private readonly IDelayStrategy _speedupStrategy;
            private readonly TraceWriter _trace;
            private DateTimeOffset _lastRenewal;
            private TimeSpan _lastRenewalLatency;
            private TimeSpan _leasePeriod;
            
            public RenewLeaseCommand(ILeaseProxy leaseProxy, LeaseDefinition leaseDefinition, IDelayStrategy speedupStrategy, TraceWriter trace, TimeSpan leasePeriod)
            {
                _lastRenewal = DateTimeOffset.UtcNow;
                _leaseProxy = leaseProxy;
                _leaseDefinition = leaseDefinition;
                _speedupStrategy = speedupStrategy;
                _trace = trace;
                _leasePeriod = leasePeriod;
            }

            public async Task<TaskSeriesCommandResult> ExecuteAsync(CancellationToken cancellationToken)
            {
                TimeSpan delay;

                try
                {
                    DateTimeOffset requestStart = DateTimeOffset.UtcNow;
                    await _leaseProxy.RenewLeaseAsync(_leaseDefinition, cancellationToken);
                    _lastRenewal = DateTime.UtcNow;
                    _lastRenewalLatency = _lastRenewal - requestStart;

                    // The next execution should occur after a normal delay.
                    delay = _speedupStrategy.GetNextDelay(executionSucceeded: true);
                }
                catch (StorageException exception)
                {
                    if (exception.IsServerSideError())
                    {
                        // The next execution should occur more quickly (try to renew the lease before it expires).
                        delay = _speedupStrategy.GetNextDelay(executionSucceeded: false);
                        _trace.Warning(string.Format(CultureInfo.InvariantCulture, "Singleton lock renewal failed for blob '{0}' with error code {1}. Retry renewal in {2} milliseconds.",
                            _leaseDefinition.Name, FormatErrorCode(exception), delay.TotalMilliseconds), source: TraceSource.Execution);
                    }
                    else
                    {
                        // Log the details we've been accumulating to help with debugging this scenario
                        int leasePeriodMilliseconds = (int)_leasePeriod.TotalMilliseconds;
                        string lastRenewalFormatted = _lastRenewal.ToString("yyyy-MM-ddTHH:mm:ss.FFFZ", CultureInfo.InvariantCulture);
                        int millisecondsSinceLastSuccess = (int)(DateTime.UtcNow - _lastRenewal).TotalMilliseconds;
                        int lastRenewalMilliseconds = (int)_lastRenewalLatency.TotalMilliseconds;

                        _trace.Error(string.Format(CultureInfo.InvariantCulture, "Singleton lock renewal failed for blob '{0}' with error code {1}. The last successful renewal completed at {2} ({3} milliseconds ago) with a duration of {4} milliseconds. The lease period was {5} milliseconds.",
                            _leaseDefinition.Name, FormatErrorCode(exception), lastRenewalFormatted, millisecondsSinceLastSuccess, lastRenewalMilliseconds, leasePeriodMilliseconds));

                        // If we've lost the lease or cannot re-establish it, we want to fail any
                        // in progress function execution
                        throw;
                    }
                }
                return new TaskSeriesCommandResult(wait: Task.Delay(delay));
            }

            private static string FormatErrorCode(StorageException exception)
            {
                int statusCode;
                if (!exception.TryGetStatusCode(out statusCode))
                {
                    return "''";
                }

                string message = statusCode.ToString(CultureInfo.InvariantCulture);

                string errorCode = exception.GetErrorCode();

                if (errorCode != null)
                {
                    message += ": " + errorCode;
                }

                return message;
            }
        }

        private static string GetAccountName(SingletonAttribute attribute)
        {
            string accountName = attribute.Account;

            if (string.IsNullOrWhiteSpace(accountName))
            {
                accountName = ConnectionStringNames.Lease;
            }

            if (string.IsNullOrWhiteSpace(AmbientConnectionStringProvider.Instance.GetConnectionString(accountName)))
            {
                accountName = ConnectionStringNames.Storage;
            }

            return accountName;
        }
    }
}
