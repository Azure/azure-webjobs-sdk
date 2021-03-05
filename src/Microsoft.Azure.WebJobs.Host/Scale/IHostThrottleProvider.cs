// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    public enum ThrottleState
    {
        Unknown,
        Enabled,
        Disabled
    }

    public class HostThrottleResult
    {
        public ThrottleState ThrottleState { get; set; }
    }

    /// <summary>
    /// Interface for providing throttle signals to dynamic concurrency control.
    /// </summary>
    public interface IHostThrottleProvider
    {
        HostThrottleResult GetStatus(ILogger logger = null);
    }

    public class HostProcessThrottleProvider : IHostThrottleProvider
    {
        private readonly IHostHealthMonitor _hostHealthMonitor;

        public HostProcessThrottleProvider(IHostHealthMonitor hostHealthMonitor)
        {
            _hostHealthMonitor = hostHealthMonitor;
        }

        public HostThrottleResult GetStatus(ILogger logger = null)
        {
            var hostHealthStatus = _hostHealthMonitor.GetStatus(logger);
            ThrottleState throttleState = ThrottleState.Unknown;

            switch (hostHealthStatus)
            {
                case HostHealthStatus.Overloaded:
                    throttleState = ThrottleState.Enabled;
                    break;
                case HostHealthStatus.Ok:
                    throttleState = ThrottleState.Disabled;
                    break;
                default:
                    throttleState = ThrottleState.Unknown;
                    break;
            }

            var result = new HostThrottleResult
            {
                ThrottleState = throttleState
            };

            return result;
        }
    }
}
