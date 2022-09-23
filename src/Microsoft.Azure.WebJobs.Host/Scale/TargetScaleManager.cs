// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    internal class TargetScalerManager : ITargetScalerManager
    {
        private readonly List<ITargetScaler> _targetScalers = new List<ITargetScaler>();
        private object _syncRoot = new object();

        public TargetScalerManager()
        {
        }

        public TargetScalerManager(IEnumerable<ITargetScaler> targetScalers)
        {
            // add any initial target scalers coming from DI
            // additional monitors can be added at runtime
            // via Register
            _targetScalers.AddRange(targetScalers);
        }

        public void Register(ITargetScaler targetScaler)
        {
            lock (_syncRoot)
            {
                if (!_targetScalers.Contains(targetScaler))
                {
                    _targetScalers.Add(targetScaler);
                }
            }
        }

        public IEnumerable<ITargetScaler> GetTargetScalers()
        {
            lock (_syncRoot)
            {
                return _targetScalers.AsReadOnly();
            }
        }
    }
}
