// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// Defines a service that can be used to monitor process health for the
    /// primary host and any child processes.
    /// </summary>
    public interface IHostProcessMonitor
    {
        /// <summary>
        /// Register the specified child process for monitoring.
        /// </summary>
        /// <param name="process">The process to register.</param>
        void RegisterChildProcess(Process process);

        /// <summary>
        /// Unregister the specified child process from monitoring.
        /// </summary>
        /// <param name="process">The process to unregister.</param>
        void UnregisterChildProcess(Process process);

        /// <summary>
        /// Get the current host health status.
        /// </summary>
        /// <param name="logger">If specified, results will be logged to this logger.</param>
        /// <returns>The status.</returns>
        HostProcessStatus GetStatus(ILogger logger = null);
    }
}
