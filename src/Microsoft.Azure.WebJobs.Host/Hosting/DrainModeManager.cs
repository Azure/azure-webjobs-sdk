// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host
{
    internal class DrainModeManager : IDrainModeManager
    {
        private readonly ILogger _logger;
        private ConcurrentDictionary<Guid, CancellationTokenSource> _cancellationTokenSources;

        public DrainModeManager(ILogger<DrainModeManager> logger)
        {
            _logger = logger;
            _cancellationTokenSources = new ConcurrentDictionary<Guid, CancellationTokenSource>();
        }

        public bool IsDrainModeEnabled { get; private set; } = false;

        public ICollection<IListener> Listeners { get; private set; } = new Collection<IListener>();

        public void RegisterListener(IListener listener)
        {
            Listeners.Add(listener);
        }

        public async Task EnableDrainModeAsync(CancellationToken cancellationToken)
        {
            if (!IsDrainModeEnabled)
            {
                IsDrainModeEnabled = true;
                _logger.LogInformation("DrainMode mode enabled");

                List<Task> tasks = new List<Task>();

                _logger.LogInformation("Calling StopAsync on the registered listeners");
                foreach (IListener listener in Listeners)
                {
                    tasks.Add(listener.StopAsync(cancellationToken));
                }

                await Task.WhenAll(tasks);

                _logger.LogInformation("Call to StopAsync complete, registered listeners are now stopped");
            }
        }
    }
}