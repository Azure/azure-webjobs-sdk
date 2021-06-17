// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#nullable enable

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// This throttle provider monitors host process health.
    /// </summary>
    internal class HostHealthThrottleProvider : IConcurrencyThrottleProvider
    {
        private readonly IHostProcessMonitor _hostProcessMonitor;

        public HostHealthThrottleProvider(IHostProcessMonitor hostProcessMonitor)
        {
            _hostProcessMonitor = hostProcessMonitor ?? throw new ArgumentNullException(nameof(hostProcessMonitor));
        }

        public ConcurrencyThrottleStatus GetStatus(ILogger? logger = null)
        {
            var processStatus = _hostProcessMonitor.GetStatus(logger);

            var status = new ConcurrencyThrottleStatus
            {
                EnabledThrottles = processStatus.ExceededLimits
            };

            switch (processStatus.State)
            {
                case HostHealthState.Overloaded:
                    status.State = ThrottleState.Enabled;
                    break;
                case HostHealthState.Ok:
                    status.State = ThrottleState.Disabled;
                    break;
                default:
                    status.State = ThrottleState.Unknown;
                    break;
            }

            return status;
        }
    }
}
