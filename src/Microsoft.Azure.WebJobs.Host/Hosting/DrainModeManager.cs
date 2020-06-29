// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host
{
    internal class DrainModeManager : IDrainModeManager
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        public DrainModeManager(ILoggerFactory loggerFactory = null)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<DrainModeManager>();
        }

        public bool IsDrainModeEnabled { get; private set; } = false;

        public ICollection<IListener> Listeners { get; private set; } = new Collection<IListener>();

        public void RegisterListener(IListener listener)
        {
            Listeners.Add(listener);
        }

        public async Task EnableDrainModeAsync()
        {
            if (!IsDrainModeEnabled)
            {
                IsDrainModeEnabled = true;
                _logger.LogInformation($"DrainMode is set to {IsDrainModeEnabled}");

                List<Task> tasks = new List<Task>();

                _logger.LogInformation($"Calling StopAsync on the registered listeners");
                foreach (IListener listener in Listeners)
                {
                    tasks.Add(listener.StopAsync(CancellationToken.None));
                }

                await Task.WhenAll(tasks);

                _logger.LogInformation($"Call to StopAsync complete, registered listeners are now stopped");
            }
        }
    }
}