// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    internal class ScaleMonitorManager : IScaleMonitorManager
    {
        private readonly List<IScaleMonitor> _monitors = new List<IScaleMonitor>();
        private object _syncRoot = new object();

        public ScaleMonitorManager()
        {
        }

        public ScaleMonitorManager(IEnumerable<IScaleMonitor> monitors, IEnumerable<IScaleMonitorProvider> monitorProviders)
        {
            // add any initial monitors coming from DI
            // additional monitors can be added at runtime
            // via Register
            _monitors.AddRange(monitors);

            // add monitors coming from any registered providers
            _monitors.AddRange(monitorProviders.Select(p => p.GetMonitor()));
        }

        public void Register(IScaleMonitor monitor)
        {
            lock (_syncRoot)
            {
                if (!_monitors.Contains(monitor))
                {
                    _monitors.Add(monitor);
                }
            }
        }
        
        public IEnumerable<IScaleMonitor> GetMonitors()
        {
            lock (_syncRoot)
            {
                return _monitors.AsReadOnly();
            }
        }
    }
}
