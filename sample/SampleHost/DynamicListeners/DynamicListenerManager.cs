// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;

namespace SampleHost
{
    internal class DynamicListenerManager : IDisposable
    {
        private readonly IDynamicListenerStatusProvider _statusProvider;
        private readonly List<DynamicListenerConfig> _dynamicListenerConfigs;
        private readonly ILogger _logger;
        private readonly IScaleMonitorManager _scaleMonitorManager;
        private readonly ITargetScalerManager _targetScalerManager;

        private bool _disposed;
        private IListener _initialListener;

        private List<IScaleMonitor> _scaleMonitors;
        private object _scaleMonitorsSyncRoot;
        private List<ITargetScaler> _targetScalers;
        private object _targetScalersSyncRoot;

        public DynamicListenerManager(IDynamicListenerStatusProvider statusProvider, ILogger<DynamicListenerManager> logger, IScaleMonitorManager monitorManager, ITargetScalerManager targetScalerManager)
        {
            _statusProvider = statusProvider;
            _dynamicListenerConfigs = new List<DynamicListenerConfig>();
            _logger = logger;
            _scaleMonitorManager = monitorManager;
            _targetScalerManager = targetScalerManager;

            InitializeScaleMonitorCollections();
        }

        public bool RequiresDynamicListener(string functionId)
        {
            return _statusProvider.IsDynamic(functionId);
        }

        public bool TryCreate(IFunctionDefinition functionDefinition, IListener listener, out IListener dynamicListener)
        {
            dynamicListener = null;
            string functionId = functionDefinition.Descriptor.Id;

            if (RequiresDynamicListener(functionId))
            {
                _logger.LogInformation($"Creating dynamic listener for function {functionId}");

                _initialListener = listener;
                dynamicListener = new DynamicListener(listener, functionDefinition, this);

                var config = new DynamicListenerConfig
                {
                    Listener = (DynamicListener)dynamicListener,
                    ScaleMonitor = GetScaleMonitor(functionId),
                    TargetScaler = GetTargetScaler(functionId)
                };
                _dynamicListenerConfigs.Add(config);

                return true;
            }

            return false;
        }

        private async Task<DynamicListenerStatus> OnListenerStarted(IListener listener)
        {
            // once the outer listener is started, we can start our dynamic monitoring
            DynamicListener dynamicListener = (DynamicListener)listener;
            string functionId = dynamicListener.FunctionId;

            var status = await _statusProvider.GetStatusAsync(functionId);
            var config = _dynamicListenerConfigs.Single(c => c.Listener == dynamicListener);

            if (!status.IsEnabled)
            {
                _logger.LogInformation($"Dynamic listener for function {functionId} will be initially disabled.");

                UpdateScaleMonitoring(config, enable: false);
            }

            // schedule the next check
            config.Timer = new Timer(OnTimer, config, (int)status.NextInterval.TotalMilliseconds, Timeout.Infinite);

            return status;
        }

        private void OnListenerStopped(IListener listener)
        {
            var config = _dynamicListenerConfigs.Single(c => c.Listener == listener);
            config.Timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void DisposeListener(string functionId, IListener listener)
        {
            _statusProvider.DisposeListener(functionId, listener);
        }

        private async void OnTimer(object state)
        {
            DynamicListenerConfig dynamicListenerConfig = (DynamicListenerConfig)state;
            var dynamicListener = dynamicListenerConfig.Listener;

            if (dynamicListener.IsStopped)
            {
                // once a listener is stopped (because the host has shut down has been put in drain mode)
                // we don't want to restart it, and we want to stop monitoring it
                dynamicListenerConfig.Timer?.Change(Timeout.Infinite, Timeout.Infinite);
                return;
            }

            // get the current status
            string functionId = dynamicListener.FunctionId;
            var status = await _statusProvider.GetStatusAsync(dynamicListener.FunctionId);
            
            if (status.IsEnabled && dynamicListener.IsStarted && !dynamicListener.IsRunning)
            {
                _logger.LogInformation($"Restarting dynamic listener for function {functionId}");

                // listener isn't currently running because either it started in a disabled state
                // or we've we've stopped it previously, so we need to restart it
                await dynamicListener.RestartAsync(CancellationToken.None);

                UpdateScaleMonitoring(dynamicListenerConfig, enable: true);
            }
            else if (!status.IsEnabled && dynamicListener.IsRunning)
            {
                _logger.LogInformation($"Stopping dynamic listener for function {functionId}. Next status check in {status.NextInterval.TotalMilliseconds}ms.");

                // we need to pause the listener
                await dynamicListener.PauseAsync(CancellationToken.None);

                UpdateScaleMonitoring(dynamicListenerConfig, enable: false);
            }

            // set the next interval
            SetTimerInterval(dynamicListenerConfig.Timer, (int)status.NextInterval.TotalMilliseconds);
        }

        /// <summary>
        /// Enables or disables scale monitoring for the specified listener.
        /// </summary>
        /// <remarks>
        /// When we stop the dynamic listener for a function across all host instances, we also need to stop
        /// scale monitoring for that function to ensure the platform doesn't continue to scale out for a function
        /// that isn't running.
        /// </remarks>
        private void UpdateScaleMonitoring(DynamicListenerConfig listenerConfig, bool enable)
        {
            if (listenerConfig.ScaleMonitor != null)
            {
                UpdateScalerCollection(_scaleMonitors, _scaleMonitorsSyncRoot, listenerConfig.ScaleMonitor, enable);
            }

            if (listenerConfig.TargetScaler != null)
            {
                UpdateScalerCollection(_targetScalers, _targetScalersSyncRoot, listenerConfig.TargetScaler, enable);
            }
        }

        private void UpdateScalerCollection<TScaler>(IList<TScaler> scalerCollection, object syncLock, TScaler scaler, bool enable)
        {
            lock (syncLock)
            {
                if (enable)
                {
                    scalerCollection.Add(scaler);
                }
                else
                {
                    scalerCollection.Remove(scaler);
                }
            }
        }

        /// <summary>
        /// Some temporary private reflection allowing us to dynamically register/unregister scale monitors as needed
        /// when function listeners are started/stopped.
        /// </summary>
        private void InitializeScaleMonitorCollections()
        {
            var scaleMonitorsFieldInfo = _scaleMonitorManager.GetType().GetField("_monitors", BindingFlags.NonPublic | BindingFlags.Instance);
            var scaleMonitorsSyncRootFieldInfo = _scaleMonitorManager.GetType().GetField("_syncRoot", BindingFlags.NonPublic | BindingFlags.Instance);

            _scaleMonitors = (List<IScaleMonitor>)scaleMonitorsFieldInfo.GetValue(_scaleMonitorManager);
            _scaleMonitorsSyncRoot = scaleMonitorsSyncRootFieldInfo.GetValue(_scaleMonitorManager);

            var targetScalersFieldInfo = _targetScalerManager.GetType().GetField("_targetScalers", BindingFlags.NonPublic | BindingFlags.Instance);
            var targetScalersSyncRootFieldInfo = _targetScalerManager.GetType().GetField("_syncRoot", BindingFlags.NonPublic | BindingFlags.Instance);

            _targetScalers = (List<ITargetScaler>)targetScalersFieldInfo.GetValue(_targetScalerManager);
            _targetScalersSyncRoot = targetScalersSyncRootFieldInfo.GetValue(_targetScalerManager);
        }

        private IScaleMonitor GetScaleMonitor(string functionId)
        {
            return _scaleMonitorManager.GetMonitors().SingleOrDefault(p => string.Compare(p.Descriptor.FunctionId, functionId, StringComparison.OrdinalIgnoreCase) == 0);
        }

        private ITargetScaler GetTargetScaler(string functionId)
        {
            return _targetScalerManager.GetTargetScalers().SingleOrDefault(p => string.Compare(p.TargetScalerDescriptor.FunctionId, functionId, StringComparison.OrdinalIgnoreCase) == 0);
        }

        private void SetTimerInterval(Timer timer, int dueTime)
        {
            if (!_disposed)
            {
                if (timer != null)
                {
                    try
                    {
                        timer.Change(dueTime, Timeout.Infinite);
                    }
                    catch (ObjectDisposedException)
                    {
                        // might race with dispose
                    }
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    foreach (var config in _dynamicListenerConfigs)
                    {
                        config.Timer?.Dispose();
                    }
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

        private class DynamicListener : IListener
        {
            private readonly IFunctionDefinition _functionDefinition;
            private readonly IListener _initialListener;
            private readonly DynamicListenerManager _dynamicListenerManager;
            private IListener _activeListener;
            private bool _started;
            private bool _stopped;
            private bool _isRunning;

            public bool IsRunning => _isRunning;

            public bool IsStarted => _started;

            public bool IsStopped => _stopped && _started;

            public DynamicListener(IListener listener, IFunctionDefinition functionDefinition, DynamicListenerManager dynamicListenerManager)
            {
                _initialListener = _activeListener = listener;
                _functionDefinition = functionDefinition;
                _dynamicListenerManager = dynamicListenerManager;
            }

            public string FunctionId => _functionDefinition.Descriptor.Id;

            public IListenerFactory ListenerFactory => _functionDefinition.ListenerFactory;

            public void Cancel()
            {
                _activeListener.Cancel();
            }

            public void Dispose()
            {
                _activeListener.Dispose();
            }

            public async Task StartAsync(CancellationToken cancellationToken)
            {
                // Once the host starts the listener, we need to notify the manager
                // and start tracking the listener's status. We don't want to start any
                // monitoring before this.
                var status = await _dynamicListenerManager.OnListenerStarted(this);

                if (status.IsEnabled)
                {
                    await _activeListener.StartAsync(cancellationToken);
                    _isRunning = true;
                }

                _started = true;
            }

            public async Task StopAsync(CancellationToken cancellationToken)
            {
                // when the host is stopping listeners (e.g. as part of host shutdown)
                // we want to stop monitoring.
                _dynamicListenerManager.OnListenerStopped(this);

                await _activeListener?.StopAsync(cancellationToken);

                _stopped = true;
                _isRunning = false;
            }

            public async Task RestartAsync(CancellationToken cancellationToken)
            {
                if (_activeListener == null)
                {
                    // If we're restarting, we've previously stopped the active listener.
                    // We need to create and start a new listener.
                    _activeListener = await ListenerFactory.CreateAsync(CancellationToken.None);
                }
                
                await _activeListener.StartAsync(CancellationToken.None);

                _isRunning = true;
            }

            public Task PauseAsync(CancellationToken cancellationToken)
            {
                // we need to stop the active listener and dispose it
                _ = Orphan(_activeListener);

                _activeListener = null;
                _isRunning = false;

                return Task.CompletedTask;
            }

            private async Task Orphan(IListener listener)
            {
                await listener.StopAsync(CancellationToken.None);

                if (!object.ReferenceEquals(listener, _initialListener))
                {
                    // For any listeners we've created, we need to dispose them
                    // ourselves.
                    // The initial listener is owned by the host and will be disposed
                    // externally.
                    _dynamicListenerManager.DisposeListener(FunctionId, listener);
                }
            }
        }

        private class DynamicListenerConfig
        {
            public DynamicListener Listener { get; set; }
            
            public IScaleMonitor ScaleMonitor { get; set; }

            public ITargetScaler TargetScaler { get; set; }

            public Timer Timer { get; set; }
        }
    }
}
